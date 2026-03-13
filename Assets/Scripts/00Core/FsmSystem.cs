using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public interface IState<T> where T : Enum
    {
        T Id { get; }
        FsmSystem<T> Fsm { get; set; }
        void Enter(T id);

        void In();
        void LoginFrame();
        void Exit(T id);
        void Switch();
        bool CanSwitch();
        void UnInit();
    }


    public class FsmSystem<T> where T : Enum
    {
        private Dictionary<T, IState<T>> _stateDic;
        private T _nowStateId;
        private T _oldStateId;

        private IState<T> _nowState;

        public T Id => _nowStateId;

        public T OldId => _oldStateId;

        #region 初始化

        private FsmSystem(params IState<T>[] values)
        {
            _stateDic = new Dictionary<T, IState<T>>();
            for (int i = 0; i < values.Length; i++)
            {
                Add(values[i]);
            }
            _nowState = values[0];
            _oldStateId = values[0].Id;
            _oldStateId = values[0].Id;
        }

        private void Add(IState<T> state)
        {
            if (state == null) return;
            if (_stateDic.ContainsKey(state.Id)) return;
            state.Fsm = this;
            _stateDic.Add(state.Id, state);
        }

        private void Remove(T id)
        {
            if (!_stateDic.ContainsKey(id)) return;
            _stateDic[id].UnInit();
            _stateDic.Remove(id);
        }
        public void Clear()
        {
            foreach (var item in _stateDic.Values)
            {
                item.UnInit();
            }
            _stateDic.Clear();
        }

        #endregion
        #region 应用
        public void Update()
        {
            _nowState.In();
        }
        public void LoginFrame()
        {
            _nowState.LoginFrame();
            if (_nowState.CanSwitch()) _nowState.Switch();
        }
        public void Trans(T id)
        {
            if (!_stateDic.ContainsKey(id)) return;
            _nowState.Exit(id);
            _oldStateId = _nowStateId;
            _nowStateId = id;
            _nowState = _stateDic[id];
            _nowState.Enter(_oldStateId);
        }
        #endregion
        /*
        public static FsmSystem<UnitState> CreateStuFsm(StudentController unit)
        {
            var fsm = new FsmSystem<UnitState>(
                new Stu_State_Grand(unit),
                new Stu_State_Move(unit),
                new Stu_State_Patrol(unit),
                new Stu_State_Chase(unit),
                new Stu_State_Attack(unit),
                new Stu_state_Flee(unit),
                new Stu_State_Kill(unit),
                new Stu_State_Deadline(unit),
                new Stu_State_Jump(unit),
                new Stu_State_Show(unit)
           );
            return fsm;
        }

        public static FsmSystem<UnitState> CreatePlayerFsm(PlayerController unit)
        {
            var fsm = new FsmSystem<UnitState>(
                new Player_State_Normal(unit),
                new Player_State_Hiding(unit),
                new Player_State_Jump(unit),
                new Player_State_Show(unit)
            );
            return fsm;
        }*/
    }
}