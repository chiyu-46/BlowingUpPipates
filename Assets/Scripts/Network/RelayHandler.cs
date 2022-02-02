using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Authentication;
using Unity.Services.Core.Environments;

public class RelayHandler : MonoBehaviour
{
    [SerializeField]
    [Tooltip("环境是与您的项目关联的 Unity 游戏服务数据的逻辑分区，允许您保存和分离从开发阶段到生产阶段的内容。")]
    private string environment = "production";
    [SerializeField]
    [Tooltip("客户端将允许与之通信的最大连接数。分配服务也使用这个值来寻找一个有足够容量的中继服务器。")]
    private int m_MaxConnections = 4;
    [SerializeField] 
    [Tooltip("创建中继服务的首选区域。")]
    private string region;

    /// <summary>
    /// 对主机和连接玩家进行认证。
    /// </summary>
    /// <returns>PlayerId</returns>
    public async Task<string> AuthenticatePlayer()
    {
        InitializationOptions options = new InitializationOptions().SetEnvironmentName(environment);

        await UnityServices.InitializeAsync(options);
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        return AuthenticationService.Instance.PlayerId;
    }
        
    /// <summary>
    /// 获取分配的中继服务并获取JoinCode。
    /// </summary>
    /// <param name="maxConnections">客户端将允许与之通信的最大连接数。分配服务也使用这个值来寻找一个有足够容量的中继服务器</param>
    /// <param name="region">创建中继服务的首选区域</param>
    /// <returns>参考 https://docs.unity.com/relay/SDK/Unity.Services.Relay.Models.htm</returns>
    public static async Task<(string ipv4address, ushort port, byte[] allocationIdBytes, byte[] connectionData, byte[] key, string joinCode)>
        AllocateRelayServerAndGetJoinCode(int maxConnections, string region = null)
    {
        Allocation allocation;
        string createJoinCode;
        try
        {
            allocation = await Relay.Instance.CreateAllocationAsync(maxConnections, region);
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay create allocation request failed {e.Message}");
            throw;
        }

        Debug.Log($"server: {allocation.ConnectionData[0]} {allocation.ConnectionData[1]}");
        Debug.Log($"server: {allocation.AllocationId}");

        try
        {
            createJoinCode = await Relay.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch
        {
            Debug.LogError("Relay create join code request failed");
            throw;
        }

        return (allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.ConnectionData, allocation.Key, createJoinCode);
    }

    /// <summary>
    /// 使用 joinCode 加入中继服务。
    /// </summary>
    /// <param name="joinCode">Host 玩家与其他玩家共享的加入代码。</param>
    /// <returns>参考 https://docs.unity.com/relay/SDK/Unity.Services.Relay.Models.htm</returns>
    public static async Task<(string ipv4address, ushort port, byte[] allocationIdBytes, byte[] connectionData, byte[] hostConnectionData, byte[] key)> 
        JoinRelayServerFromJoinCode(string joinCode)
    {
        JoinAllocation allocation;
        try
        {
            allocation = await Relay.Instance.JoinAllocationAsync(joinCode);
        }
        catch
        {
            Debug.LogError("Relay create join code request failed");
            throw;
        }

        Debug.Log($"client: {allocation.ConnectionData[0]} {allocation.ConnectionData[1]}");
        Debug.Log($"host: {allocation.HostConnectionData[0]} {allocation.HostConnectionData[1]}");
        Debug.Log($"client: {allocation.AllocationId}");

        return (allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port, allocation.AllocationIdBytes, allocation.ConnectionData, allocation.HostConnectionData, allocation.Key);
    }

    /// <summary>
    /// 用于身份验证的协程。
    /// </summary>
    public IEnumerator AuthenticatePlayerCoroutine()
    {
        var authenticateTask = AuthenticatePlayer();
        while (!authenticateTask.IsCompleted)
        {
            yield return null;
        }
        if (authenticateTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when authenticate player. Exception: " + authenticateTask.Exception.Message);
            yield break;
        }
        yield return null;
    }
    
    /// <summary>
    /// 作为 Host 开始游戏。
    /// </summary>
    public IEnumerator StartAsHost()
    {
        var serverRelayUtilityTask = AllocateRelayServerAndGetJoinCode(m_MaxConnections, region);
        while (!serverRelayUtilityTask.IsCompleted)
        {
            yield return null;
        }
        if (serverRelayUtilityTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when attempting to start Relay Server. Server not started. Exception: " + serverRelayUtilityTask.Exception.Message);
            yield break;
        }

        var (ipv4address, port, allocationIdBytes, connectionData, key, joinCode) = serverRelayUtilityTask.Result;
        // Todo:向用户显示joinCode。
        GetComponent<NetworkHandler>().ShowJoinCode(joinCode);
        
        // 当启动一个中继服务器时，两个实例的连接数据是相同的。
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(ipv4address, port, allocationIdBytes, key, connectionData);
        NetworkManager.Singleton.StartHost();
        yield return null;
    }
    
    /// <summary>
    /// 作为 Client 开始游戏。
    /// </summary>
    /// <param name="relayJoinCode">Host 玩家与其他玩家共享的加入代码。</param>
    public IEnumerator StartAsClient(string relayJoinCode)
    {
        // Todo:通过用户界面获取RelayJoinCode
        var clientRelayUtilityTask = JoinRelayServerFromJoinCode(relayJoinCode);

        while (!clientRelayUtilityTask.IsCompleted)
        {
            yield return null;
        }

        if (clientRelayUtilityTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when attempting to connect to Relay Server. Exception: " + clientRelayUtilityTask.Exception.Message);
            yield break;
        }

        var (ipv4address, port, allocationIdBytes, connectionData, hostConnectionData, key) = clientRelayUtilityTask.Result;

        // 当作为客户端连接到中继服务器时，connectionData和hostConnectionData是不同的。
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(ipv4address, port, allocationIdBytes, key, connectionData, hostConnectionData);

        NetworkManager.Singleton.StartClient();
        yield return null;
    }
}
