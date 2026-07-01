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
        AimIK ik;
        VehicleWeaponsManager manager= GetComponent<VehicleWeaponsManager>();
        if (data.weaponRights.Length > 0)
        {
            var rightModel = Instantiate(data.weaponRights[arch.rightWeaponIndex].go, weaponPointR);
            rightModel.transform.localPosition = Vector3.zero;
            rightModel.transform.localRotation = Quaternion.identity;
            
            if (rightModel.transform.TryGetComponentInChildren(out ik))
            {
                ik.solver.target = LookPoint;
            }
            manager.AddWeapon(rightModel.GetComponent<WeaponController>(), data.weaponRights[arch.rightWeaponIndex].battleIcon);
        }
       
        if (data.weaponLefts.Length > 0)
        {
            var leftModel = Instantiate(data.weaponLefts[arch.leftWeaponIndex].go, weaponPointL);
            leftModel.transform.localPosition = Vector3.zero;
            leftModel.transform.localRotation = Quaternion.identity;
            if (leftModel.transform.TryGetComponentInChildren(out ik))
            {
                ik.solver.target = LookPoint;
            }
            manager.AddWeapon(leftModel.GetComponent<WeaponController>(), data.weaponLefts[arch.leftWeaponIndex].battleIcon);
        }
        mpb = new(transform);
        mpb.Set("_BaseMap", data.Diffs[arch.skinIndex].texture)
        .Set("_BlendingScale", arch.blendScale.RawFloat)
        .Set("_BlendingMap", data.Blends[arch.blendIndex].texture).Apply();

        Destroy(this);
    }
}
