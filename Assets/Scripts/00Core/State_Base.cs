using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Core
{
    public abstract class State_Base<T> : IState<T>
    where T : Enum
    {
        protected readonly T _id;
        protected FsmSystem<T> _fsm;

        protected State_Base(T id)
        {
            _id = id;
        }
        FsmSystem<T> IState<T>.Fsm
        {
            get => _fsm;
            set => _fsm = value;
        }
        T IState<T>.Id => _id;

        public abstract void Enter(T oldId);
        public abstract void In();
        public abstract void LoginFrame();
        public abstract void Exit(T newId);
        protected abstract bool IsAction();
        public abstract void Switch();
        public abstract bool CanSwitch();
        public abstract void UnInit();

    }
}