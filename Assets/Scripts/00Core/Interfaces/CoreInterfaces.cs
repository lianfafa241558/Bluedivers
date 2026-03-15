using System.Collections;
using System.Collections.Generic;
using PEMaths;
using UnityEngine;

namespace Core.Interface
{
    public interface I_GlobaManager
    {
        void Init();
        void UnInit();
    }

    public interface I_Entity
    {
        public string ShowName { get; set; }
        public string Id { get; set; }
        public Sprite Portrait { get; set; }
        public Sprite ExtraPortrait { get; set; }

        public Color Color { get; set; }

        PEVector2 LogicPos { get; }

        PEVector3 Logic3Pos { get; }

        Vector3 CenterPos { get; }
        Vector3 Pos { get; set; }
        Vector3 Angles { get; }

        /// <summary>单位半径</summary>
        public float HalfRange { get; }

        public Transform transform { get; }
        public GameObject gameObject { get; }
    }

}
