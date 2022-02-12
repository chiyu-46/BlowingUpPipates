using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletMovement : MonoBehaviour
{
    [SerializeField][Tooltip("子弹飞行速度")]
    private float speed = 10f;

    private void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}
