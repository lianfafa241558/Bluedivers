using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AI;
using Unity.BaseTool;
using Core;

namespace Utils
{
    public static class Tool
    {

#if UNITY_EDITOR
        private static DrawLabelUtils drawLabelUtils;
#endif

        private static ExchangeTransformManager exchangeArea;
        static Tool()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && GameObject.Find("GameRoot"))
            {
                drawLabelUtils = new GameObject("DrawLabelUtils").AddComponent<DrawLabelUtils>();
                drawLabelUtils.transform.parent = GameObject.Find("GameRoot").transform;
            }
#endif
            if (Application.isPlaying && GameObject.Find("GameRoot"))
            {
                exchangeArea = new GameObject("ExchangeArea").AddComponent<ExchangeTransformManager>();
                exchangeArea.transform.parent = GameObject.Find("GameRoot").transform;
                exchangeArea.transform.position = Vector3.down * 1000;
            }
        }

        //语法糖：
        //if(obj is Health hp&&hp.nowhp>0)
        //可以写成:if(obj is Health{nowhp: >0})

        //var array = new[] { 10, 20, 30, 40, 50 };
        //Debug.Log(array[^1]); // 输出最后一个元素: 50
        //Debug.Log(string.Join(", ", array[1..^1])); // 输出: 20, 30, 40

        #region 几何
        /// <summary>
        /// 角度归一化函数，确保角度在(-180,180)范围内
        /// </summary>
        public static float NormalizeAngle(float angle)
        {
            angle %= 360;
            if (angle > 180) angle -= 360;
            return angle;
        }

        /// <summary>
        /// 碰撞体上的随机一点
        /// </summary>
        public static Vector3 RandomBoundsPoint(this Collider collider, out Quaternion normal)
        {
            Vector3 pos = collider.ClosestPointOnBounds(collider.transform.position + RandomVctor3() * 10);

            Bounds bounds = collider.bounds;
            Vector3 center = bounds.center;
            Vector3 pointLocal = pos - center;

            // 计算各轴向绝对偏移量
            float xDist = Mathf.Abs(pointLocal.x);
            float yDist = Mathf.Abs(pointLocal.y);
            float zDist = Mathf.Abs(pointLocal.z);
            Vector3 dir;
            // 确定主导轴
            if (xDist > yDist && xDist > zDist)
                dir = new Vector3(Mathf.Sign(pointLocal.x), 0, 0);
            else if (yDist > zDist)
                dir = new Vector3(0, Mathf.Sign(pointLocal.y), 0);
            else
                dir = new Vector3(0, 0, Mathf.Sign(pointLocal.z));
            normal = Quaternion.FromToRotation(Vector3.up, dir);
            return pos;
        }

        /// <summary>
        /// 碰撞体内的随机一点
        /// </summary>
        public static Vector3 RandomPoint(this Collider collider, out Quaternion normal)
        {
            Vector3 pos = collider.ClosestPoint(collider.transform.position + RandomVctor3() * 10);

            Bounds bounds = collider.bounds;
            Vector3 center = bounds.center;
            Vector3 pointLocal = pos - center;

            // 计算各轴向绝对偏移量
            float xDist = Mathf.Abs(pointLocal.x);
            float yDist = Mathf.Abs(pointLocal.y);
            float zDist = Mathf.Abs(pointLocal.z);
            Vector3 dir;
            // 确定主导轴
            if (xDist > yDist && xDist > zDist)
                dir = new Vector3(Mathf.Sign(pointLocal.x), 0, 0);
            else if (yDist > zDist)
                dir = new Vector3(0, Mathf.Sign(pointLocal.y), 0);
            else
                dir = new Vector3(0, 0, Mathf.Sign(pointLocal.z));
            normal = Quaternion.FromToRotation(Vector3.up, dir);
            return pos;
        }
        public static Vector3 RandomVctor3()
        {
            return Random.rotation * Vector3.forward;
        }

        public static float Distance(this Collider collider, Vector3 pos)
        {
            return Vector3.Distance(collider.ClosestPointOnBounds(pos), pos);
        }
        public static float Distance(this Collider a, Collider b)
        {
            return Vector3.Distance(a.ClosestPointOnBounds(b.transform.position), b.ClosestPointOnBounds(a.transform.position));
        }


        #endregion

        #region 数学


        /// <summary>
        /// 转成任意进制数
        /// </summary>
        public static string ToBase(int value, int toBase) => System.Convert.ToString(value, toBase);

        /// <summary>
        /// 按位取有多少个1
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int CountOnes(this int number)
        {
            int count = 0;
            while (number > 0)
            {
                number &= (number - 1); // 清除最低位的1
                ++count;
            }
            return count;
        }

        public static string FloatToTime(float value)
        {
            int Minutes = (int)(value / 60);
            int Seconds = (int)((value + 0.5f) % 60);
            return string.Format("{0:D2}:{1:D2}", Minutes, Seconds);

        }
        /// <summary>
        /// 整数转罗马数字
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string IntToRoman(int value)
        {
            int[] nums = new int[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] romans = new string[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            string result = "";
            int start = 0;
            while (value > 0)
            {
                for (int i = start, l = nums.Length; i < l; ++i)
                {
                    if (value >= nums[i])
                    {
                        value -= nums[i];
                        result += romans[i];
                        start = i;
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 将分贝值转换为百分比浮点数(0分贝=100)
        /// </summary>
        /// <param name="dB">分贝值</param>
        /// <returns>百分比浮点数(0-100范围)</returns>
        public static float DBToPet(float dB)
        {
            // 分贝转线性值的公式：value = 10^(dB/20)
            return Mathf.Pow(10f, dB / 20f) * 100;
        }
        /// <summary>
        /// 将百分比浮点数转换为分贝值(1=0dB)
        /// </summary>
        /// <param name="percentage">百分比值(0-100范围)</param>
        /// <returns>分贝值(负无穷到0范围)</returns>
        public static float PetToDB(float percentage)
        {
            // 防止对0取对数导致负无穷
            if (percentage <= 1f)
                return -80f; // 返回一个合理的下限值

            // 分贝转换公式：dB = 20 * log10(percentage)
            return 20f * Mathf.Log10(percentage / 100);
        }

        public static string FillZero(int value, int digit)
        {
            string re = value.ToString();
            while (re.Length < digit) re = "0" + re;
            return re;
        }
        /// <summary>
        /// 判断一个Enum值是否在(a,b)开区间内
        /// </summary>
        public static bool In(this System.Enum value, System.Enum a, System.Enum b)
        {
            int valueInt = System.Convert.ToInt32(value);
            int aInt = System.Convert.ToInt32(a);
            int bInt = System.Convert.ToInt32(b);

            int min = Mathf.Min(aInt, bInt);
            int max = Mathf.Max(aInt, bInt);

            return valueInt > min && valueInt < max;
        }

        /// <summary>
        /// 判断一个值是否在(a,b)开区间内
        /// </summary>
        public static bool In(int value, int a, int b)
        {
            return value > Mathf.Min(a, b) && value < Mathf.Max(a, b);
        }
        public static bool In(float value, float a, float b, float allowError = 0)
        {
            return value > Mathf.Min(a, b) - allowError && value < Mathf.Max(a, b) + allowError;
        }
        public static bool In(Vector3 value, Vector3 a, Vector3 b)
        {
            return value.x > Mathf.Min(a.x, b.x) && value.x < Mathf.Max(a.x, b.x) &&
                    value.y > Mathf.Min(a.y, b.y) && value.y < Mathf.Max(a.y, b.y) &&
                    value.z > Mathf.Min(a.z, b.z) && value.z < Mathf.Max(a.z, b.z);
        }
        public static bool In2D(Vector3 value, Vector3 a, Vector3 b)
        {
            return value.x > Mathf.Min(a.x, b.x) && value.x < Mathf.Max(a.x, b.x) &&
                    value.y > Mathf.Min(a.y, b.y) && value.y < Mathf.Max(a.y, b.y);
        }
        public static bool In3D(Vector3 value, Vector3 a, Vector3 b)
        {
            return value.x > Mathf.Min(a.x, b.x) && value.x < Mathf.Max(a.x, b.x) &&
                    value.y > Mathf.Min(a.y, b.y) && value.y < Mathf.Max(a.y, b.y) &&
                    value.z > Mathf.Min(a.z, b.z) && value.z < Mathf.Max(a.z, b.z);
        }


        //public static float LoginDis(Vector3 a, Vector3 b) => Vector2.Distance(new(a.x, a.z), new(b.x, b.z));
        /// <summary>
        /// 返回X位小数
        /// </summary>
        public static float Round(float value, int digit = 1) => Mathf.Round(value * Mathf.Pow(10, digit)) * Mathf.Pow(10, -digit);
        public static Vector3 Round(Vector3 value, int digit = 1) => new(Round(value.x, digit), Round(value.y, digit), Round(value.z, digit));

        /// <summary>
        /// 夹角度数(0-360)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public static float VectorAngle(Vector2 from, Vector2 to)
        {
            float angle;
            Vector3 cross = Vector3.Cross(from, to);
            angle = Vector2.Angle(from, to);
            return cross.z < 0 ? 360 - angle : angle;
        }
        /// <summary>正余数</summary>
        public static int PositiveRemainder(int value, int remainder)
        {
            return (value + remainder) % remainder;
        }



        /// <summary>
        /// 点到线的距离
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="target"></param>
        /// <param name="front"></param>
        /// <returns></returns>
        public static float PointToLineDis(Vector3 p1, Vector3 p2, Vector3 target, bool front = true)
        {
            Vector3 p1_2 = p2 - p1;//p1->p2的向量
            Vector3 p1_target = target - p1;//p1->target向量
                                            //Debug.LogWarning("夹角"+Vector3.Angle(p1_2, p1_target));
            p1_2.y = 0;
            p1_target.y = 0;
            if (front && Vector3.Angle(p1_2, p1_target) > 90) return 99;
            Vector3 p1f = Vector3.Project(p1_target, p1_2);//计算投影p1->f
            return Vector3.Distance(target, p1f + p1);// 加上p1坐标 然后计算距离
        }


        /// <summary>
        /// 点到点的距离？
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static float PointToVectorDis(Vector3 p1, Vector3 p2) => Vector3.Project(p2, p1).magnitude;

        public static float ColorDistance(Color a, Color b) => Vector3.Distance(new(a.r, a.g, a.b), new(b.r, b.g, b.b));

        public static Color ColorLerp(Color a, Color b, float speed) => new(Mathf.Lerp(a.r, b.r, speed), Mathf.Lerp(a.g, b.g, speed), Mathf.Lerp(a.b, b.b, speed));




        private static Color[] colorList = { new(0.58f, 0.02f, 0.03f), new(0.75f, 0.54f, 0.01f), new(0.12f, 0.44f, 0.6f), new(0.58f, 0.28f, 0.64f), Color.grey };



        public static bool Calculate(this CompareOperate operate, float source, float target)
        {
            return operate switch {
                CompareOperate.Equal => Mathf.Approximately(source, target),//这个是相似的方法
                CompareOperate.NotEqual => !Mathf.Approximately(source, target),
                CompareOperate.Less => source < target,
                CompareOperate.LessEqual => source <= target,
                CompareOperate.Greater => source > target,
                CompareOperate.GreaterEqual => source >= target,
                CompareOperate.Contain => (int)source != 0 && ((int)source & (int)target) == (int)target,
                CompareOperate.NotContain => (int)source != 0 && ((int)source & (int)target) == 0,
                _ => throw new System.ArgumentException("找不到操作符" + operate),
            };
        }

        public static float PreventZero(this float value)
    => value <= 0 ? 1 : value;

        public static float Difference(float a,float b)=> Mathf.Abs(a - b);

        #endregion

        #region 坐标系转换

        public static Vector3 ScreenSize => new(Screen.width, Screen.height, 0);

        public static Vector2 ScreenSize2D => new(Screen.width, Screen.height);

        public static float ScreenAspect => Screen.width / (float)Screen.height;


        /// <summary>
        ///世界坐标转屏幕坐标
        /// </summary>
        /// <returns></returns>
        public static Vector3 WorldPosToScreenPos(Vector3 vector)
        {
            var re = Camera.main.WorldToScreenPoint(vector);
            return re * Mathf.Sign(re.z);
        }

        /// <summary>
        /// 鼠标位置转世界坐标
        /// </summary>
        /// <returns></returns>
        public static Vector3 MouthPosToWorldPos()
        {
            Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100);
            return hit.point;
        }
        /// <summary>
        /// 坐标在屏幕内
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        public static bool IsScreenVisible(Vector3 worldPos)
        {
            if (!Camera.main.IsValid()) return false;
            Vector3 viewPos = Camera.main.WorldToViewportPoint(worldPos);
            bool inViewport = viewPos.x >= 0 && viewPos.x <= 1 && viewPos.y >= 0 && viewPos.y <= 1;
            bool inFront = Vector3.Dot(Camera.main.transform.forward, (worldPos - Camera.main.transform.position).normalized) > 0;
            return inViewport && inFront;
        }

        /// <summary>
        /// 判断b是否在a的前方
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        //public static bool InFront(Transform a, Transform b)=> Vector3.Angle(b.position - a.position, a.forward) < 90f;
        public static bool InFront(Transform a, Transform b) => Vector3.Dot(b.position - a.position, a.forward) > 0;


        public static float NavDis(Vector3 now, Vector3 to)
        {
            NavMeshPath path = new();
            //让y≡0，保证寻路走得到
            NavMesh.CalculatePath(now - new Vector3(0, now.y, 0), to - new Vector3(0, to.y, 0), NavMesh.AllAreas, path);
            //agent.CalculatePath(vector,path);
            float dis = 0;
            //=1就是走不到，直接在原地
            if (path.corners.Length > 1)
            {
                for (int i = 0; i < path.corners.Length - 1; i++) dis += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                dis += Vector3.Distance(path.corners[path.corners.Length - 1], to);
            }
            Debug.DrawLine(to, to + new Vector3(0, 3, 0), Color.yellow, 5f);

            return dis;
        }
        public static Vector3 Clamp(Vector3 value, Vector3 a, Vector3 b) => new(
            Mathf.Clamp(value.x, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x)),
            Mathf.Clamp(value.y, Mathf.Min(a.y, b.y), Mathf.Max(a.y, b.y)),
            Mathf.Clamp(value.z, Mathf.Min(a.z, b.z), Mathf.Max(a.z, b.z)));


        /// <summary>
        /// 限制在一个圆角矩形(近似)内(输入值需要标准化)
        /// </summary>
        public static Vector2 ClampRoundedRectangle(Vector2 value, Vector2 ellipse, Vector2 rectangle)
        {
            //value = GetEllipseIntersection(value, ellipse.x, ellipse.y);
            value = FindEllipseIntersection(value, ellipse);
            value.x = Mathf.Clamp(value.x, -rectangle.x, rectangle.x);
            value.y = Mathf.Clamp(value.y, -rectangle.y, rectangle.y);
            return value;
        }

        /// <summary>
        /// 矢量和椭圆形的交点(宽度和高度都是半径)
        /// </summary>
        public static Vector2 GetEllipseIntersection(Vector2 v, float width, float height)
        {
            if (v == Vector2.zero)
                return Vector2.zero;

            float vx = v.x;
            float vy = v.y;

            // 计算 t^2
            float denominator = (vx * vx) / width / width + (vy * vy) / height / height;

            // 如果 denominator 为 0 或小于 0，说明没有交点
            Debug.LogWarning("限制器在椭圆内" + (denominator <= 0) + " " + denominator);
            if (denominator <= 0)
            {
                return v; // 返回原始矢量
            }

            float tSquared = 1 / denominator;
            float t = Mathf.Sqrt(tSquared);
            return t * v; // 返回交点
        }


        public static Vector2 FindEllipseIntersection(Vector2 v, Vector2 ellipse)
        {
            if (v == Vector2.zero)
                return Vector2.zero;

            float a = ellipse.x;
            float b = ellipse.y;

            float denominatorX = a * a;
            float denominatorY = b * b;

            float s = (v.x * v.x) / denominatorX + (v.y * v.y) / denominatorY;

            if (s <= 1)
                return v;

            float t = 1f / Mathf.Sqrt(s);

            return t <= 1f ? new Vector2(v.x * t, v.y * t) : v;
        }

        /// <summary>
        ///矢量是否在圆角矩形内
        ///先判断是否在矩形内，然后判断是否在椭圆内
        /// </summary>
        public static bool InRoundedRectangle(Vector2 v, Vector2 ellipse, Vector2 rectangle)
        {
            if (!In2D(v, -rectangle, rectangle))
            {
                return false;
            }
            float denominator = (v.x * v.x) / (ellipse.x * ellipse.x) + (v.y * v.y) / (ellipse.y * ellipse.y);
            // 如果 denominator 为 0 或小于 0，说明没有交点
            //Debug.LogWarning("在椭圆内"+(denominator <= 0)+" "+ denominator);
            return denominator <= 1;
        }


        public static float Mapping01(float min, float max, float value) => (value - min) / (max - min);
        public static float Mapping(float min, float max, float value, float scale) => (value - min) / (max - min) * scale;
        public static float Mapping(float min, float max, float scale) => min + (max - min) * scale;
        public static float Mapping(Vector2 map, float scale) => map.x + (map.y - map.x) * scale;


        public static Vector2 Mapping01(Vector2 ldPoint, Vector2 rtPoint, Vector2 value) => (value - ldPoint) / (rtPoint - ldPoint);
        public static Vector2 Mapping(Vector2 ldPoint, Vector2 rtPoint, Vector2 value, Vector2 newMap) => (value - ldPoint) / (rtPoint - ldPoint) * newMap;

        /// <summary>
        /// vector相乘
        /// </summary>
        /// <param name="a">坐标A</param>
        /// <param name="b">坐标B</param>
        /// <returns></returns>
        public static Vector3 Mult(this Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);

        //vector2本身就是逐一相乘
        //public static Vector2 Mult(this Vector2 a, Vector2 b) => new Vector3(a.x * b.x, a.y * b.y);

        /// <summary>
        /// vector相除
        /// </summary>
        /// <param name="a">坐标A</param>
        /// <param name="b">坐标B</param>
        /// <returns></returns>
        public static Vector3 Div(this Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        public static Vector2 Div(this Vector2 a, Vector2 b) => new Vector3(a.x / b.x, a.y / b.y);



        public static Vector2 Vector3To2(Vector3 vector) => new(vector.x, vector.z);
        public static Vector3 Vector2To3(Vector2 vector) => new(vector.x, 0, vector.y);

        /// <summary>
        /// 线段到线段的距离
        /// </summary>
        /// <param name="seg1Start"></param>
        /// <param name="seg1End"></param>
        /// <param name="seg2Start"></param>
        /// <param name="seg2End"></param>
        /// <returns></returns>
        public static float LineToLineDis(Vector3 seg1Start, Vector3 seg1End, Vector3 seg2Start, Vector3 seg2End)
        {
            Vector3 closestPoint1 = PointToLinePoint(seg1Start, seg1End, seg2Start);
            Vector3 closestPoint2 = PointToLinePoint(seg1Start, seg1End, seg2End);

            float distance1 = Vector3.Distance(closestPoint1, seg2Start);
            float distance2 = Vector3.Distance(closestPoint2, seg2End);

            float distance = Mathf.Min(distance1, distance2);
            return distance;
        }
        /// <summary>
        /// 点到线的最近点
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static Vector3 PointToLinePoint(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            direction.Normalize();

            float t = Mathf.Clamp01(Vector3.Dot(point - start, direction) / length);
            Vector3 closestPoint = start + t * direction * length;

            return closestPoint;
        }


        public static Vector2 RectLocPos(RectTransform rect, Vector2 vector, ShapeType shape = ShapeType.Rectangle)
        {
            Vector2 size = rect.sizeDelta * 0.5f;
            Vector2 pos = vector - rect.pivot;
            pos.y *= -1;
            //Debug.LogWarning("输入"+ vector + "相对位置" + pos);
            switch (shape)
            {
                case ShapeType.Circle:
                    //宽高的平均值 * 方向
                    return (size.x + size.y) * 0.5f * pos.normalized;
                case ShapeType.Ellipse:
                    //* 方向
                    return size * pos.normalized;
                case ShapeType.Prismatic:
                    //* 方向/方向的长度
                    return size * pos / (Mathf.Abs(pos.x) + Mathf.Abs(pos.y));
                default:
                    return size * pos;
            }
        }

        public static Vector2 RectLocPos(RectTransform rect, TextAnchor anchor, ShapeType shape = ShapeType.Rectangle)
        {
            return RectLocPos(rect, new Vector2(((int)anchor % 3) * 0.5f, Mathf.Floor((int)anchor / 3) * 0.5f), shape);
        }


        public static Vector2Int ToInt(this Vector2 a) => new Vector2Int(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y));
        public static Vector3Int ToInt(this Vector3 a) => new Vector3Int(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), Mathf.RoundToInt(a.z));

        public static Vector2 ToFloat(this Vector2Int a) => new Vector2(a.x, a.y);
        public static Vector3 ToIntFloat(this Vector3Int a) => new Vector3(a.x, a.y, a.z);

        public static Vector2 ToVector2(this Vector3 vector) => new(vector.x, vector.z);
        public static Vector3 ToVector3(this Vector2 vector) => new(vector.x, 0, vector.y);

        #endregion

        #region 字符串
        public static string Ksegmentation(float value) => ((int)value).ToString("N0");
        public static int TextLength(string text, int size = 1)
        {
            float re = 0;
            for (int i = 0, l = text.Length; i < l; ++i)
            {
                if (text[i] > 127)
                    re += size;
                else
                    re += size * 0.5f;
            }
            return Mathf.CeilToInt(re);
        }

        private static Dictionary<char, float> textwidth = new() {
            ['!'] = 0.5f,
            ['@'] = 1.75f,
            ['%'] = 1.5f,
            ['^'] = 1.25f,
            ['&'] = 1.5f,
            ['*'] = 0.75f,
            ['('] = 0.5f,
            [')'] = 0.5f,
            ['-'] = 0.75f,
            ['+'] = 1.25f,
            ['_'] = 0.75f,
            ['='] = 1.25f,
            [','] = 0.4f,
            ['.'] = 0.4f,
            ['/'] = 0.75f,
            ['?'] = 0.8f,
            [';'] = 0.4f,
            [':'] = 0.4f,
        };
        public static int TextLength(string text, float letter, float digit, float symbol, float chinese)
        {
            float re = 0;
            for (int i = 0, l = text.Length; i < l; ++i)
            {
                char c = text[i];
                if (char.IsLetter(c))
                {
                    // 字母
                    re += letter;
                }
                else if (char.IsDigit(c))
                {
                    // 数字
                    re += digit;
                }
                else if (char.IsLetterOrDigit(c))
                {
                    // 其他字母数字字符（如中文）
                    re += chinese;
                }
                else if (textwidth.TryGetValue(c, out var width))
                {
                    re += digit * width;
                }
                else
                {
                    // 符号和其他字符
                    re += symbol;
                }
            }
            return Mathf.FloorToInt(re);
        }

        public static KeyCode GetKeyDownCode()
        {
            if (Input.anyKey)
            {
                foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKey(keyCode))
                    {
                        //Debug.Log(keyCode.ToString());
                        return keyCode;
                    }
                }
            }
            return KeyCode.None;
        }
        #endregion

        #region 颜色
        /// <summary>
        /// 颜色的亮度
        /// </summary>
        public static float ColorLight(this Color color) => Mathf.Max(color.r, color.g, color.b);

        public static Color ColorMin(Color a, Color b) => ColorLight(a) < ColorLight(b) ? a : b;
        public static Color ColorMin(float a, Color b) => a < ColorLight(b) ? a * Color.white : b;
        public static Color ColorMin(Color a, float b) => ColorLight(a) < b ? a : b * Color.white;


        public static Color ColorMax(Color a, Color b) => ColorLight(a) > ColorLight(b) ? a : b;
        public static Color ColorMax(float a, Color b) => a > ColorLight(b) ? a * Color.white : b;
        public static Color ColorMax(Color a, float b) => ColorLight(a) > b ? a : b * Color.white;
        public static Color MultiplyRGB(this Color color, float multiplier)
        {
            return new Color(
                color.r * multiplier,
                color.g * multiplier,
                color.b * multiplier,
                color.a
            );
        }


        #endregion

        #region 动画
        /// <summary>
        /// 获取动画片段长度
        /// </summary>
        /// <param name="anim"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static float GetAnimStateLenght(Animator anim, string name)
        {
            float scale = anim.speed;
            float re = 0;
            RuntimeAnimatorController controller = anim.runtimeAnimatorController;
            var states = controller.animationClips;
            for (int i = 0; i < states.Length; i++)
            {
                //Debug.LogWarning("动画的名称"+ states[i].name);
                if (states[i].name.Contains(name))
                {
                    re += states[i].length / scale;
                }
            }

            /*
            UnityEditor.Animations.AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            //获取层级中的状态机
            UnityEditor.Animations.ChildAnimatorState[] states = stateMachine.states;
            //遍历状态机
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name.Contains(name))
                {
                    Motion motion = states[i].state.motion;
                    AnimationClip clip = motion as AnimationClip;
                    re += clip.length / scale / states[i].state.speed;
                }
            }*/

            return re;
        }

        public static void SetState(this AudioSource source, bool state)
        {
            if (state) source.Play();
            else source.Stop();
        }
        #endregion

        #region 附加蒙皮

        /// <summary>
        /// 设置部件，将target附加到origin上
        /// </summary>
        /// <param name="origin">原组件</param>
        /// <param name="target">想要替换上去的组件</param>
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
                if (!haveBone)//如果没有就补充
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
            //origin.rootBone = origin.bones[0].parent;//设置根骨骼
            origin.bones = bones.ToArray();//复制骨骼//问题出在这里，骨骼数量必须一致
            origin.sharedMesh = target.sharedMesh;//好像是复制模型
            origin.sharedMaterials = target.sharedMaterials;  //复制材质

        }

        /// <summary>
        /// 使用递归查找对应的骨骼
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

        #endregion

        #region 枚举
        /// <summary>
        /// 字符串转枚举
        /// </summary>
        /// <param name="str">字符串</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="ignoreCase">大小写敏感性</param>
        /// <returns></returns>
        public static T StringToEnum<T>(string str, T defaultValue=default, bool ignoreCase = true) where T : struct, System.Enum
        {
            // 空值/空字符串直接返回默认值
            if (string.IsNullOrEmpty(str))
            {
                return defaultValue;
            }

            // 尝试转换，失败返回默认值
            if (System.Enum.TryParse(str, ignoreCase, out T result))
            {
                return result;
            }
            else
            {
                Debug.LogWarning($"字符串「{str}」无法转换为枚举{typeof(T).Name}，使用默认值：{defaultValue}");
                return defaultValue;
            }
        }

        public static string GetEnumString(this System.Enum value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());
            var attribute = fieldInfo.GetCustomAttributes(typeof(CustomLabelAttribute), false);
            return attribute.Length > 0 ? ((CustomLabelAttribute)attribute[0]).name : value.ToString();

        }


        public static T EnumValue<T>(this System.Enum rank, T E, T D, T C, T B, T A, T S)
        {
            switch (System.Convert.ToInt32(rank))
            {
                case 0: return E;
                case 1: return D;
                case 2: return C;
                case 3: return B;
                case 4: return A;
                case 5: return S;
            }
            return C;
        }
        public static int GetEnumIndex<T>(this T enumValue)
        {
            T[] values = (T[])System.Enum.GetValues(typeof(T));
            return System.Array.IndexOf(values, enumValue);
        }

        public static int EnumLenght<T>()
        {
            return System.Enum.GetValues(typeof(T)).Length;
        }

        public static void ForEachFlag<T>(this T e, System.Action<T> action) where T : System.Enum
        {
            for (int i = 0; i <= System.Enum.GetValues(e.GetType()).Length; i++)
            {
                if ((System.Convert.ToInt32(e) & (1 << i)) != 0)
                {
                    action.Invoke(e);
                }
            }
        }

        #endregion

        #region MPB

        public static MaterialPropertyBlock mpb;

        public static void SetColor(this MeshRenderer mr, Color color)
        {
            if (!mpb.IsValid()) mpb = new();
            mpb.SetColor("_Color", color);
            mr.SetPropertyBlock(mpb);
            mpb.Clear();
        }

        #endregion
        
        #region 绘制

        public static void DrawLabel(Vector3 pos, string text, float time, Color color)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawLabel(pos, color, text, time);
#endif
        }
        public static void DrawLabel(Vector3 pos, string text, float time)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawLabel(pos, Color.white, text, time);
#endif
        }

        public static void DrawShape(ShapeType shape, Vector3 pos, Vector3 size, float time, Color color)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawShape(shape, pos, color, size, time);
#endif
        }
        public static void DrawShape(ShapeType shape, Vector3 pos, Vector3 size, float time)
        {
#if UNITY_EDITOR
            drawLabelUtils.DrawShape(shape, pos, Color.white, size, time);
#endif
        }

        #endregion

        #region 置换
        public static void Exchange(Transform source)
        {
            exchangeArea.Exchange(source);
        }
        public static Transform GetExchangeArea()
        {
            return exchangeArea.transform;
        }

        #endregion

        public static bool Contains(this LayerMask mask, int layout)
            =>(mask.value & layout) != 0;


        public static void Destroy(Object obj, float t = 0)
        {
            //if (!obj.IsValid()) return;
            Debug.Log("销毁了物体" + obj + "延迟" + t + "秒");
            Object.Destroy(obj, t);
        }


        internal static bool IsValid(this UnityEngine.Object obj)
  => obj != null && !obj.Equals(null);
        internal static bool IsValid(this System.Object obj)
            => obj != null && !obj.Equals(null);


    }
}