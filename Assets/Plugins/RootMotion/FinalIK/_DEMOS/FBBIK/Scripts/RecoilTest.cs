﻿using UnityEngine;
using System.Collections;
using RootMotion.FinalIK;

namespace RootMotion.Demos
{
    /// <summary>
    /// Just for testing out the Recoil script.
    /// </summary>
    [RequireComponent(typeof(Recoil))]
    public class RecoilTest : MonoBehaviour
    {
        public float magnitude = 1f;
        private Recoil recoil;

        void Start()
        {
            recoil = GetComponent<Recoil>();
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0)) recoil.Fire(magnitude);
        }
    }
}