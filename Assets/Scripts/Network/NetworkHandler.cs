using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkHandler : MonoBehaviour
{
    private NetworkManager networkManager;
    [SerializeField] 
    private InputField joinCodeInputField;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }

    public void StartHostWithLan()
    {
        networkManager.StartHost();
    }

    public void StartClientWithLan()
    {
        networkManager.StartClient();
    }

    public void StartHostWithWan()
    {
        StartCoroutine(GetComponent<RelayHandler>().StartAsHost());
    }
    
    public void StartClientWithWan()
    {
        //Todo:获取加入代码。
        string relayJoinCode = joinCodeInputField.text;
        StartCoroutine(GetComponent<RelayHandler>().StartAsClient(relayJoinCode));
    }

    public void ShowJoinCode(string relayJoinCode)
    {
        joinCodeInputField.text = relayJoinCode;
    }
}
