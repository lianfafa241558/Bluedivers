using System;
using System.Collections.Generic;
using GameContract;

public interface IEquippable
{
    string ID { get; }
    public I_Actor Owner { get; }

    void OnInstall(I_Actor actor, Func<IEnumerable<IEquippable>> getEquippableList);
    void OnUninstall();

    /// <summary>装备其他装备时触发，如果true让控制器卸载自己</summary>
    bool NeedUninstall(IEquippable newEquip); 
    /// <summary>(给控制器注入用的)装备即将销毁时触发</summary>
    event Action<IEquippable> OnEquipDestroy;
}
