using System;
using System.Collections.Generic;
using FPSGame.Core;
using FPSGame.GameContract;

using UnityEngine;

namespace FPSGame.Game
{
    /// <summary>
    /// 维护场上所有单位的注册与查询。
    /// </summary>
    [AddComponentMenu("管理/单位注册表")]
    public class ActorsManager : Singleton<ActorsManager>
    {
        public static Queue<KVP<UnitTypeEnum, IActor>> OnActorCreat=new();//用来创建跟随UI的

        public static List<IActor> Enemys { get; private set; } = new();
        public static List<IActor> Actors { get; private set; } = new();
        public static List<IActor> Players { get; private set; } = new();
        public static List<IActor> SpecUnits { get; private set; } = new();


        public static IActor Player { get; private set; }
        public void RegisterPlayer(IActor player)
        {
            Player = player;
            Players.Add(player);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Player, player));
            //Debug.LogError("玩家出生" + player + "  " + player.transform.position, player.transform);
        }
        public void RegisterFriend(Actor friend)
        {
            Players.Add(friend);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Friend, friend));
        }
        public void RegisterSpecUnit(Actor specUnit)
        {
            //Debug.LogError("响应特殊单位出生"+ specUnit, specUnit);
            SpecUnits.Add(specUnit);
            OnActorCreat.Enqueue(new(UnitTypeEnum.SpecUnit, specUnit));
        }
        public void RegisterEnemy(Actor specUnit)
        {
            //Debug.LogError("特殊单位出生"+ specUnit, specUnit);
            Enemys.Add(specUnit);
        }
        public void UnRegisterUnit(Actor actor)
        {
            switch (actor.Type)
            {
                case UnitTypeEnum.Player:
                    //Players.Remove(actor);
                    break;
                case UnitTypeEnum.Friend:
                    //Players.Remove(actor);
                    break;
                case UnitTypeEnum.Enemy:
                    Enemys.Remove(actor);
                    Actors.Remove(actor);
                    break;
                case UnitTypeEnum.SpecUnit:
                    SpecUnits.Remove(actor);
                    Actors.Remove(actor);
                    break;
                case UnitTypeEnum.Other:
                    SpecUnits.Remove(actor);
                    Actors.Remove(actor);
                    break;
            }

        }

        public override void Awake()
        {
            //不管这个新的单例会不会被覆盖，都刷新列表
            Actors = new();
            Players = new();
            SpecUnits = new();
            OnActorCreat = new();
            base.Awake();
            UnitEventSub.OnPlayerCreate += RegisterPlayer;
            UnitEventSub.OnFriendCreate += RegisterFriend;
            UnitEventSub.OnSpecUnitCreate += RegisterSpecUnit;
            UnitEventSub.OnUnitDeath += UnRegisterUnit;
            UnitEventSub.OnEnemyCreate += RegisterEnemy;
        }
        private void OnDestroy()
        {
            UnitEventSub.OnPlayerCreate -= RegisterPlayer;
            UnitEventSub.OnFriendCreate -= RegisterFriend;
            UnitEventSub.OnSpecUnitCreate -= RegisterSpecUnit;
            UnitEventSub.OnUnitDeath -= UnRegisterUnit;
            UnitEventSub.OnEnemyCreate -= RegisterEnemy;
        }
    }

}
