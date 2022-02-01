using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkHandler : MonoBehaviour
{
    private NetworkManager networkManager;

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();
    }

    public void StartHost()
    {
        networkManager.StartHost();
    }
    
    public void StartServer()
    {
        networkManager.StartServer();
    }
    
    public void StartClient()
    {
        networkManager.StartClient();
    }
}
