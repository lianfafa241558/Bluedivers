using System.Collections;
using System.Collections.Generic;
using Core;
using GameContract;
using UnityEngine;

public class InterestPoint : BaseObject, I_MissionPoint
{

    bool I_MissionPoint.FollowAreaScale => false;

    float I_MissionPoint.IconSizeScale => 0.5f;

    float I_MissionPoint.AreaRange { get => 0; set{ } }
}