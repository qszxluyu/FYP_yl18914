using UnityEngine;
using System.Collections;

public class FollowTarget : MonoBehaviour
{


    public float distanceAway = 10;          // 摄像机距离跟随物体背后的距离
    public float distanceUp = 2;            // 距离物体的最小距离
    public float smooth = 3;                // 摄像机移动平滑指数
    //public Transform follow;             //通过赋值取得物体（1-1）
    private Vector3 targetPosition;     // the position the camera is trying to be in

    //主摄像机（有时候会在工程中有多个摄像机，但是只能有一个主摄像机吧）     

    Transform follow;

    void Start()
    {
        follow = GameObject.Find("RacketPviot").transform;//通过名字找寻物体
                                                     // follow = GameObject.FindWithTag("Car").transform;//通过标签找寻物体

    }

    void LateUpdate()
    {
        // 设置追踪目标的坐标作为调整摄像机的偏移量
        targetPosition = follow.position + Vector3.up * distanceUp - follow.forward * distanceAway;

        // 在摄像机和被追踪物体之间制造一个顺滑的变化
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smooth);

        //设置视野中心是目标物体
        transform.LookAt(follow);
    }
}
