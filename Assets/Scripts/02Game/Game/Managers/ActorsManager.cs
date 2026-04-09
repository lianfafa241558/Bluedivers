using System;
using System.Collections.Generic;
using Core;
using GameContract;
using Unity.BaseTool;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class ActorsManager : Singleton<ActorsManager>
    {
        public static Queue<KVP<UnitTypeEnum, I_Actor>> OnActorCreat=new();//用来创建跟随UI的

        public static List<I_Actor> Actors { get; private set; } = new();
        public static List<I_Actor> Players { get; private set; } = new();
        public static List<I_Actor> SpecUnits { get; private set; } = new();


        public static I_Actor Player { get; private set; }
        public void RegisterPlayer(I_Actor player)
        {
            //Debug.LogError("玩家出生" + player+"  "+player.transform.position, player.transform);
            Player = player;
            Players.Add(player);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Player, player));
        }
        public void RegisterFriend(Actor friend)
        {
            Players.Add(friend);
            OnActorCreat.Enqueue(new(UnitTypeEnum.Friend, friend));
        }
        public void RegisterSpecUnit(Actor specUnit)
        {
            //Debug.LogError("特殊单位出生"+ specUnit, specUnit);
            SpecUnits.Add(specUnit);
            OnActorCreat.Enqueue(new(UnitTypeEnum.SpecUnit, specUnit));
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
            base.Awake();
            GlobalEventManager.OnPlayerCreate += RegisterPlayer;
            GlobalEventManager.OnFriendCreate += RegisterFriend;
            GlobalEventManager.OnSpecUnitCreate += RegisterSpecUnit;
            GlobalEventManager.OnUnitDeath += UnRegisterUnit;
        }
        private void OnDestroy()
        {
            GlobalEventManager.OnPlayerCreate -= RegisterPlayer;
            GlobalEventManager.OnFriendCreate -= RegisterFriend;
            GlobalEventManager.OnSpecUnitCreate -= RegisterSpecUnit;
            GlobalEventManager.OnUnitDeath -= UnRegisterUnit;
        }
    }

}
