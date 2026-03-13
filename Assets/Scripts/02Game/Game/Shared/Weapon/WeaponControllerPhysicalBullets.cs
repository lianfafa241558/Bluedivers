using System.Collections;
using System.Collections.Generic;
using Unity.BaseTool;
using UnityEngine;

namespace Unity.FPS.Game
{
    public class WeaponControllerPhysicalBullets: MonoBehaviour
    {

        [Foldout("子弹参数", true)]

        [CustomLabel("子弹壳预制体")]
        public GameObject ShellCasing;
        [CustomLabel("弹子弹的点位")]
        public Transform EjectionPort;
        [CustomLabel("施加在壳体上的力")]
        [Range(0.0f, 5.0f)]
        public float ShellCasingEjectionForce = 2.0f;
        [CustomLabel("实体子弹池的初始数量")]
        [Range(1, 30)] public int ShellPoolSize = 1;
        [CustomLabel("持续时间")]
        [Range(1, 30)] public int ShellDuration = 5;

        private AutoObjectPool<KVP<Rigidbody,float>> m_PhysicalAmmoPool;

        private WeaponBaseController m_weapon;

        public void Start()
        {
            m_weapon = GetComponent<WeaponBaseController>();
            m_weapon.OnShoot += ShootShell;
            m_PhysicalAmmoPool = new (_Update, _Add, _Pop, _Push, ShellPoolSize);

        }
        private void OnDestroy()
        {
            m_weapon.OnShoot -= ShootShell;
            m_PhysicalAmmoPool.UnInit();
            m_PhysicalAmmoPool = null;
        }

        private void Update()
        {
            m_PhysicalAmmoPool.Update();
        }

        private bool _Update(KVP<Rigidbody, float> item)
        {
            item.Value -= Time.deltaTime;
            return item.Value>0;
        }
        private KVP<Rigidbody, float> _Add()
        {
            GameObject shell = Instantiate(ShellCasing, transform);
            shell.SetActive(false);
            return new(shell.GetComponent<Rigidbody>(),ShellDuration);
        }
        private void _Pop(KVP<Rigidbody, float> item)
        {
            item.Value = ShellDuration;
            Rigidbody rigidbody = item.Key;
            rigidbody.transform.parent = null;
            rigidbody.gameObject.SetActive(true);

            rigidbody.transform.position = EjectionPort.transform.position;
            rigidbody.transform.rotation = EjectionPort.transform.rotation;

            rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rigidbody.AddForce(rigidbody.transform.up * ShellCasingEjectionForce, ForceMode.Impulse);

        }
        private void _Push(KVP<Rigidbody, float> item)
        {
            Rigidbody rigidbody = item.Key;
            rigidbody.transform.parent = transform;
            rigidbody.gameObject.SetActive(false);
        }

        void ShootShell(WeaponBaseController weapon)
        {
            m_PhysicalAmmoPool.Get();
        }


    }
}