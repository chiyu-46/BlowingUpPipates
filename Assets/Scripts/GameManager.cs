using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Demo
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField]
        private GameObject startPanel;
        [SerializeField]
        private GameObject panelForLan;
        [SerializeField]
        private GameObject panelForWan;
    
        [Space(10)] 
        [SerializeField]
        private GameObject lanManager;
        [SerializeField]
        private GameObject wanManager;
    
        public void UseLan()
        {
            startPanel.SetActive(false);
            panelForLan.SetActive(true);
            lanManager.SetActive(true);
        }
        
        public void UseWan()
        {
            startPanel.SetActive(false);
            panelForWan.SetActive(true);
            wanManager.SetActive(true);
            StartCoroutine(wanManager.GetComponent<RelayHandler>().AuthenticatePlayerCoroutine());
        }
    }
}

