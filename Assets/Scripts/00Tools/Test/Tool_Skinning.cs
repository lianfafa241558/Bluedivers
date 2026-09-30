using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using FPSGame.Core;
using FPSGame.Attributes;

namespace FPSGame.Utils
{
    public static partial class Tool
{
        /// <summary>
        /// 设置部件，将target附加到origin ?
        /// </summary>
        /// <param name="origin">原组 ?/param>
        /// <param name="target">想要替换上去的组 ?/param>
        public static void AdditionPart(SkinnedMeshRenderer origin, SkinnedMeshRenderer target)
        {

            Transform[] bonesSkin = target.bones;
            List<Transform> bones = new List<Transform>();
            bool haveBone;
            //重新填充骨骼
            foreach (Transform item in bonesSkin)
            {
                haveBone = false;
                foreach (Transform part in origin.bones)
                {
                    if (part && part.name == item.name)
                    {

                        haveBone = true;
                        bones.Add(part);
                        break;
                    }
                }
                if (!haveBone)
                {
                    foreach (Transform part in origin.rootBone.parent)
                    {
                        if (part.name == item.name)
                        {

                            bones.Add(part);
                            haveBone = true;
                            break;
                        }
                    }
                }
                if (!haveBone)//如果没有就补 ?
                {

                    Transform parent = FillParentBones(item, bones);
                    if (!parent) parent = origin.rootBone;
                    var go = new GameObject(item.name);
                    go.transform.parent = parent;
                    go.transform.localPosition = item.transform.localPosition;
                    go.transform.localRotation = item.transform.localRotation;

                    bones.Add(go.transform);
                }
            }

            //origin.transform.position = target.transform.position;
            //origin.rootBone = origin.bones[0].parent;//设置根骨 ?
            origin.bones = bones.ToArray();//复制骨骼//问题出在这里，骨骼数量必须一 ?
            origin.sharedMesh = target.sharedMesh;//好像是复制模 ?
            origin.sharedMaterials = target.sharedMaterials;  //复制材质

        }

        /// <summary>
        /// 使用递归查找对应的骨 ?
        /// </summary>
        /// <param name="skin"></param>
        /// <param name="bones"></param>
        /// <returns></returns>
        private static Transform FillParentBones(Transform skin, List<Transform> bones)
        {
            if (!skin) return null;
            Transform parent = bones.FirstOrDefault(t => t.name == skin.parent.name);
            if (parent == null)
            {
                parent = FillParentBones(skin.parent, bones);
            }
            return parent;
        }

    }
}
