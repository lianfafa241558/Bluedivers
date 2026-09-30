using System.Collections;
using System.Collections.Generic;
using FPSGame.GameContract;
using UnityEngine;
using FPSGame.Utils;

namespace FPSGame.Game
{

public class AutoDead : MonoBehaviour
{

    protected IHealth m_Health;

    protected virtual void Start()
    {
        m_Health =GetComponent<IHealth>();

        m_Health.OnDie += _OnDie;
    }

    protected virtual void _OnDie(GameObject source)
    {
        m_Health.OnDie -= _OnDie;
        Tool.Destroy(gameObject, 0);

    }

}
}
