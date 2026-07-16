using RootMotion.FinalIK;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using Utils;

public class BattleApplyVehicleData : MonoBehaviour
{

    public VehicleData_SO data;
    public Transform weaponPointL, weaponPointR;
    public Transform LookPoint;
    [HideInInspector]
    public MpbController mpb;

    private void Awake()
    {
        var arch = ArchiveSvc.Archive.VehicleCustomDic[data.vehicleName];
        // 防止存档 index 越界
        int rightIdx = Mathf.Clamp(arch.rightWeaponIndex, 0, data.weaponRights.Length - 1);
        int leftIdx = Mathf.Clamp(arch.leftWeaponIndex, 0, data.weaponLefts.Length - 1);
        int skinIdx = Mathf.Clamp(arch.skinIndex, 0, data.Diffs.Length - 1);
        int blendIdx = Mathf.Clamp(arch.blendIndex, 0, data.Blends.Length - 1);

        AimIK ik;
        VehicleWeaponsManager manager = GetComponent<VehicleWeaponsManager>();
        if (data.weaponRights.Length > 0)
        {
            var rightModel = Instantiate(data.weaponRights[rightIdx].go, weaponPointR);
            rightModel.transform.localPosition = Vector3.zero;
            rightModel.transform.localRotation = Quaternion.identity;

            if (rightModel.transform.TryGetComponentInChildren(out ik))
            {
                ik.solver.target = LookPoint;
            }
            manager.AddWeapon(rightModel.GetComponent<WeaponController>(), data.weaponRights[rightIdx].battleIcon);
        }

        if (data.weaponLefts.Length > 0)
        {
            var leftModel = Instantiate(data.weaponLefts[leftIdx].go, weaponPointL);
            leftModel.transform.localPosition = Vector3.zero;
            leftModel.transform.localRotation = Quaternion.identity;
            if (leftModel.transform.TryGetComponentInChildren(out ik))
            {
                ik.solver.target = LookPoint;
            }
            manager.AddWeapon(leftModel.GetComponent<WeaponController>(), data.weaponLefts[leftIdx].battleIcon);
        }
        mpb = new(transform);
        mpb.Set("_BaseMap", data.Diffs[skinIdx].texture)
        .Set("_BlendingScale", arch.blendScale.RawFloat)
        .Set("_BlendingMap", data.Blends[blendIdx].texture).Apply();

        Destroy(this);
    }
}
