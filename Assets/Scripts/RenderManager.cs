using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using TMPro;
using DG.Tweening;


[Serializable]
public class Car
{
    public int index;
    public int position;
    public int state;
    public int torque;
    public int rpm;
    public int speed;
    public bool isPedal;
    public bool isCut;

    public bool prev_Cut = false;

    public int Position {
        get {
            return position;
        }
    }
}

public enum CutState
{
    None = 0,
    CutIn = 1,
    Cut = 2,
    CutOut = 3
}

public class RenderManager : MonoBehaviour
{
    UdpClient sendSocket;
    UdpClient receiveSocket;
    IPEndPoint serverEndPoint;

    public GameObject[] prefabs;

    public GameObject road;
    public GameObject car_Cut;
    public GameObject car_Cut_object;

    public CutState state_cut = 0;   // 0 : Normal, 1 : Cut in Animation, 2 : Cut, 3 : Cut out Animation

    private Dictionary<string, Car> cars = new Dictionary<string, Car>();
    private Dictionary<int, Transform> models = new Dictionary<int, Transform>();

    private List<GameObject> roads = new List<GameObject>();

    private Queue<string> queue_Data = new Queue<string>();

    public Cinemachine.CinemachineVirtualCamera vCamera;
    public Cinemachine.CinemachineTargetGroup targetGroup;
    
    public string server_ip = "127.0.0.1";
    public int server_port = 9999;
    public int receive_port = 10000;

    void Start()
    {
        for(int i = 1; i <= 3; ++i) {
            GameObject newRoad = Instantiate(road);
            newRoad.transform.position = new Vector3(0, 0, i * 5000);
            roads.Add(newRoad);
        }

        ConnectToServer();
        RequestData(); // Python 서버에 데이터를 요청
    }

    Sequence cutSequence;

    private void Update()
    {
        int cutIndex = 0;
        while(queue_Data.Count > 0) {
            string jsonData = queue_Data.Dequeue();
            Dictionary<string, Car> carData = JsonConvert.DeserializeObject<Dictionary<string, Car>>(jsonData);

            List<string> keysToRemove = new List<string>();
            List<string> keysToAdd = new List<string>();

            foreach (var key in cars.Keys) {
                if (!carData.ContainsKey(key)) {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in carData.Keys) {
                if (!cars.ContainsKey(key)) {
                    keysToAdd.Add(key);
                }
            }
            foreach (var key in keysToRemove) {
                cars.Remove(key);
            }
            foreach (var car in cars) {
                if (!carData.ContainsKey(car.Key))
                    continue;
                cars[car.Key].index = carData[car.Key].index;
                cars[car.Key].position = carData[car.Key].position;
                cars[car.Key].state = carData[car.Key].state;
                cars[car.Key].torque = carData[car.Key].torque;
                cars[car.Key].rpm = carData[car.Key].rpm;
                cars[car.Key].speed = carData[car.Key].speed;
                cars[car.Key].isCut = carData[car.Key].isCut;
                cars[car.Key].isPedal = carData[car.Key].isPedal;
            }
            foreach (var key in keysToAdd) {
                Car newCar = new Car
                {
                    index = carData[key].index,
                    position = carData[key].position,
                    state = carData[key].state,
                    torque = carData[key].torque,
                    rpm = carData[key].rpm,
                    speed = carData[key].speed,
                    isCut = carData[key].isCut,
                    isPedal = carData[key].isPedal
                };
                cars.Add(key, newCar);
            }
        }

        HashSet<int> keysToLeft = new HashSet<int>();

        foreach (var key in models.Keys) {
            keysToLeft.Add(key);
        }

        bool makeCut = false;
        int cutType = 0;
        bool someone_is_joining = false;
        foreach (var car in cars) {
            int index = car.Value.index;
            if (!models.ContainsKey(index)) {
                if (car.Value.position >= 1000000) continue;
                GameObject newModel = Instantiate(prefabs[models.Count % 4]);
                models.Add(index, newModel.transform);
                newModel.transform.position = new Vector3(0, 0, (car.Value.Position - 100) * .5f);
                targetGroup.AddMember(newModel.transform, 1f, 0f);
                targetGroup.DoUpdate();
                Debug.Log($"{index} 차량 생성");
            }
            keysToLeft.Remove(index);
            Transform targetModel = models[index];
            Vector3 targetPosition = new Vector3(0, 0, car.Value.Position * .5f);
            // 위치를 부드럽게 이동 (lerp 사용)
            targetModel.position = Vector3.Lerp(targetModel.position, targetPosition, Time.deltaTime);

            if (car.Value.isCut) {
                if (!car.Value.prev_Cut) {
                    car.Value.prev_Cut = true;
                    makeCut = true;
                    cutIndex = car.Value.index;
                    cutType = 1;
                    Debug.Log($"{car.Value.index} 차량 cut 상황 돌입");
                }
                if (car.Value.state > 0)
                    someone_is_joining = true;
            }
            else {
                if (car.Value.prev_Cut) {
                    car.Value.prev_Cut = false;
                    makeCut = true;
                    cutIndex = car.Value.index;
                    cutType = 2;
                    Debug.Log($"{car.Value.index} 차량 cut 상황 해제");
                }
            }
        }

        foreach (var key in keysToLeft) {
            if (models.ContainsKey(key)) {
                Transform targetModel = models[key];
                targetGroup.RemoveMember(targetModel);
                models.Remove(key);
                Destroy(targetModel.gameObject);
            }
        }

        int first_key = 256;
        bool isKey = false;

        foreach (var key in models.Keys) {
            first_key = Math.Min(key, first_key);
            isKey = true;
        }

        if (isKey) {
            float zValue = models[first_key].transform.position.z;

            while(zValue >= roads.Count * 2500) {
                GameObject newRoad = Instantiate(road);
                newRoad.transform.position = new Vector3(0, 0, roads.Count * 5000);
                roads.Add(newRoad);
            }

            if(makeCut && cutIndex > 0) {
                if (cutType == 1) {
                    PlayCutInAnimation(models[first_key]);
                }
                else if (cutType == 2) {
                    PlayCutOutAnimation();
                }
            }
        }

        if (someone_is_joining == false) {
            // 아무도 연결 중이 아니라면
            switch (state_cut) {
                case CutState.CutIn: 
                    {
                        // 애니메이션 중이라면 그냥 끝냄
                        cutSequence.Kill();
                        Destroy(car_Cut_object);
                        car_Cut_object = null;
                    }
                    break;
                case CutState.Cut: 
                    {
                        PlayCutOutAnimation();
                    }
                    break;
            }
        }
    }

    void FixedUpdate()
    {
        RequestData();
    }

    void PlayCutInAnimation(Transform firstCar)
    {
        Transform firstCarTransform = firstCar;
        if (cutSequence.IsActive()) {
            cutSequence.Kill();
            Destroy(car_Cut_object);
            car_Cut_object = null;
        }
        state_cut = CutState.CutIn;
        car_Cut_object = Instantiate(car_Cut, firstCarTransform);
        car_Cut_object.transform.localPosition = new Vector3(
            3,
            0,
            -200 * .5f);
        cutSequence = DOTween.Sequence();
        cutSequence.
            Join(car_Cut_object.transform.DOLocalMoveX(0, 3f).SetEase(Ease.InQuad)).
            Join(car_Cut_object.transform.DOLocalMoveZ(-20 * .5f, 3f).SetEase(Ease.OutQuart)).
            OnComplete(() => state_cut = CutState.Cut).
            OnKill(() => cutSequence = null).
            Play();
    }

    void PlayCutOutAnimation()
    {
        if (cutSequence.IsActive()) {
            cutSequence.Kill();
        }
        if (car_Cut_object != null) {
            state_cut = CutState.CutOut;
            car_Cut_object.transform.localPosition = new Vector3(0, 0, -20 * .5f);
            cutSequence = DOTween.Sequence();
            cutSequence.
                Join(car_Cut_object.transform.DOLocalMoveX(3, 3f).SetEase(Ease.OutQuart)).
                Join(car_Cut_object.transform.DOLocalMoveZ(-200 * .5f, 3f).SetEase(Ease.InQuart)).
                OnComplete(() => {
                    Destroy(car_Cut_object);
                    car_Cut_object = null;
                    state_cut = CutState.None;
                }).
                OnKill(() => {
                    cutSequence = null;
                    Destroy(car_Cut_object);
                    car_Cut_object = null;
                }).
                Play();
        }
    }

    void ConnectToServer()
    {
        try {
            sendSocket = new UdpClient();
            receiveSocket = new UdpClient(receive_port);
            serverEndPoint = new IPEndPoint(IPAddress.Parse(server_ip), server_port); // Python 서버 IP와 포트
            StartReceiving();
            Debug.Log("Connected to UDP server.");
        }
        catch (Exception e) {
            Debug.LogError($"Connection error: {e.Message}");
        }
    }

    void RequestData()
    {
        try {
            byte[] requestData = { 0x00, 0x3f };
            sendSocket.Send(requestData, requestData.Length, serverEndPoint);
        }
        catch (Exception e) {
            Debug.LogError($"Data request error: {e.Message}");
        }
    }

    void StartReceiving()
    {
        receiveSocket.BeginReceive(OnReceiveData, null);
    }

    void OnReceiveData(IAsyncResult result)
    {
        IPEndPoint remoteEndPoint = null;
        byte[] data = receiveSocket.EndReceive(result, ref remoteEndPoint);
        string jsonData = Encoding.UTF8.GetString(data);
        queue_Data.Enqueue(jsonData);
        StartReceiving();
    }

    void OnApplicationQuit()
    {
        sendSocket.Close();
        receiveSocket.Close();
    }
}
