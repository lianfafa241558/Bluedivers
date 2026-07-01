using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using Utils;

public class AutoDead : MonoBehaviour
{

    protected Health m_Health;

    protected virtual void Start()
    {
        m_Health =GetComponent<Health>();

        m_Health.OnDie += _OnDie;
    }

    protected virtual void _OnDie(GameObject source)
    {
        m_Health.OnDie -= _OnDie;
        Tool.Destroy(gameObject, 0);

    }

}
