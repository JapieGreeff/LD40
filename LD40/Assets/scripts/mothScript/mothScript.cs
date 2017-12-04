using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mothScript : MonoBehaviour {
    public Transform Player;
    public static int MoveSpeed = 1;
    public int myspeed;
    public int MaxDist = 10;
    public int MinDist = 5;

    void Start()
    {

    }

    void Update()
    {
        transform.LookAt(Player);

        if (Vector3.Distance(transform.position, Player.position) >= MinDist)
        {
            myspeed = MoveSpeed;
            transform.position += transform.forward * myspeed * Time.deltaTime;
            
            if (Vector3.Distance(transform.position, Player.position) <= MaxDist)
            {
                //Here Call any function U want Like Shoot at here or something
            }

        }
    }

}
