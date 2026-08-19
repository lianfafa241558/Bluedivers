
using Unity.FPS.Game;
using UnityEngine;

public class ParabolicDebug : MonoBehaviour
{

    [Range(0.5f, 10f)]
    [InspectorName("每段距离间隔")]
    public float lenght = 1f;
    public Transform RayGo;
    private WeaponController m_weapon;

    public Transform target;
    public Vector3 LookPos;

    private void OnDrawGizmos() {

        if(!m_weapon) m_weapon = GetComponentInParent<WeaponController>();


        Gizmos.color = Color.red;
        float speed = m_weapon.CurrentSpeed;
        float gravity = m_weapon.CurrentGravity;
        //float time = lenght / speed;//相对于每次移动lenght的所需时间
        float time = 0.05f;//相对于每次移动lenght的所需时间
        var muzzle = m_weapon.GetMuzzle(0);
        if (target) {
            
            LookPos = CalculateLaunchPoint(muzzle.position, target.position, speed, gravity);
            muzzle.LookAt(LookPos);
            Gizmos.DrawCube(LookPos,Vector3.one);
            Gizmos.DrawCube(target.position, Vector3.one);
            Gizmos.DrawLine(target.position, LookPos);
            Gizmos.DrawLine(target.position, muzzle.position);
            Gizmos.DrawLine(muzzle.position, LookPos);
            Gizmos.color = Color.green;
            //Gizmos.DrawLine(muzzle.position, target.position+ maxHeight *Vector3.up);
        }
        //子弹的弹道和下面的写法是基本吻合的
        RayGo = null;
        Vector3 lastPos = muzzle.position ;
        Vector3 velocity = muzzle.forward * speed;
        for (int i = 0; i < 100; ++i) {
            Gizmos.color = Color.HSVToRGB((0.1f*i)%1,1,1);
            Gizmos.DrawLine(lastPos, lastPos + velocity * time);
            lastPos += velocity * time;

            velocity += Vector3.down * gravity  * time;

            if (Physics.Raycast(lastPos, velocity * time, out var hit, speed * time * 2, FpsHelper.GetHittableLayers(speed))) {
                RayGo = hit.transform;
                Gizmos.color = Color.HSVToRGB((0.1f *(i+1)) % 1, 1, 1);
                Gizmos.DrawLine(lastPos, hit.point);
                break;
            }

            
            
        }


    }



   /*
    public float pa1,pa2,dis;
    public float angle;

    

    [System.Serializable]
    public struct Show {
        public float angele;
        public float dis;
        public float scale;

        public Show(float angele, float dis, float scale) {
            this.angele = angele;
            this.dis = dis;
            this.scale = scale;
}
}

    public List<Show> arr=new();

    public Show showv;
    public float useAngle;
    */
    public Vector3 CalculateLaunchPoint(Vector3 startPos, Vector3 targetPos, float speed, float gravity) {


        // 计算水平距离
        Vector3 horizontalVec = new Vector3(targetPos.x - startPos.x, 0, targetPos.z - startPos.z);
        float d = horizontalVec.magnitude;
        float height = 0;
        if (gravity >= speed)
        {
            //最远可以到达的距离(45度)
            float maxx = speed * speed / gravity;
            float scale = Mathf.Clamp01(d / maxx);
            height = Mathf.Tan((-29.167f * (scale * scale) - 8.947f * scale + 87.25f) * Mathf.Deg2Rad) * d;
        }
        else
        {
            var dy = targetPos.y - startPos.y;
            var time = d / speed;
            height = dy + 0.5f * gravity * time * time;
        }
        // 返回目标点x,z坐标加上计算高度
        return new Vector3(targetPos.x, m_weapon.GetMuzzle(0).position.y + height, targetPos.z);

    }


}
