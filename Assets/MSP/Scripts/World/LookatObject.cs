using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookatObject : MonoBehaviour
{

    [SerializeField] private bool _lookAtPlayer;
    [SerializeField] private Transform _target;
    [SerializeField] private bool _lookVertical;
    [SerializeField] private bool _faceOpposite;

    void Start()
    {
        if (_lookAtPlayer)
            CheckForPlayerObject();
    }

    void CheckForPlayerObject()
    {
        _target = GameObject.FindGameObjectWithTag("MainCamera").transform;
    }

    void Update()
    {
        if (_target == null)
        {
            if (_lookAtPlayer)
                CheckForPlayerObject();
            return;
        }

        Vector3 targetPostition = new Vector3(_target.position.x, transform.position.y, _target.position.z);
        if (_lookVertical)
            targetPostition.y = _target.position.y;

        if (_faceOpposite)
            transform.LookAt(targetPostition);
        else
            transform.rotation = Quaternion.LookRotation(transform.position-targetPostition);
    }
}
