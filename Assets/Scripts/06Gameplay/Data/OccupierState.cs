using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FPSGame.Core;
using UnityEngine;

namespace FPSGame.GameData
{
    /// <summary>某个势力在某张地图上的占领度（百分比整数 0~100；UI 直接 /100 当进度条）。</summary>
    [Serializable]
    public class OccupierEntry
    {
        public string name;
        public int value;
    }

    /// <summary>一张地图的势力表。<c>mapId</c> = 地图资产名去掉 <c>MD_</c> 前缀（与 <c>TaskManager.MapData</c> 同一套键）。</summary>
    [Serializable]
    public class OccupierMapData
    {
        public string mapId;
        public List<OccupierEntry> occupiers = new List<OccupierEntry>();
    }

    /// <summary>整份势力占领度 —— 独立 json 文件的根。⚠ 字段名即 json 键，改名等于改数据格式。</summary>
    [Serializable]
    public class OccupierStateData
    {
        public List<OccupierMapData> maps = new List<OccupierMapData>();
    }

    /// <summary>
    /// 地图势力占领度（**世界状态**，独立于玩家存档）。
    ///
    /// <para>▍为什么从 <see cref="ArchivesData_SO"/> 拆出来（2026-10-27）：存档是**每人一份**的私有数据，
    /// 而占领度是**世界共享**的状态 —— 联机时必然要由房主一份下发全端，塞在个人存档里就必然"各看各的"。</para>
    ///
    /// <para>▍存储：与存档同目录的 <see cref="FileName"/>（首次启动用 Resources 模板
    /// <c>GameData/Occupier_Default.json</c> 初始化）。</para>
    ///
    /// <para>▍网络：直接收发 <see cref="ToJson"/> / <see cref="ApplyJson(string)"/> 的同一份 JSON，
    /// <b>不另建 DTO</b> —— 两端结构天然一致，不会出现"加了字段只改一半"。</para>
    /// </summary>
    public static class OccupierState
    {
        /// <summary>磁盘文件名（与存档同目录，见 <see cref="ArchivesDataBase_SO.DeletePath"/>）。</summary>
        public const string FileName = "OccupierState.json";

        /// <summary>初始模板的 Resources 路径（不带扩展名）。只在磁盘文件不存在时用一次。</summary>
        private const string TemplatePath = "GameData/Occupier_Default";

        /// <summary>「这张地图没有势力」的共享空表。⚠ 只读，调用方不得修改。</summary>
        private static readonly List<OccupierEntry> Empty = new List<OccupierEntry>();

        private static OccupierStateData _data;

        /// <summary>整份数据（首次访问自动 <see cref="Load"/>）。改数据请走 <see cref="Set"/> / <see cref="ApplyJson(string)"/>。</summary>
        public static OccupierStateData Data
        {
            get { EnsureLoaded(); return _data; }
        }

        /// <summary>某地图的势力表；没有这张地图 ⇒ 返回空表（**不建条目**，免得像 DisplayDic 索引器那样留占位）。</summary>
        public static List<OccupierEntry> Get(string mapId)
        {
            EnsureLoaded();
            var map = FindMap(mapId);
            return map != null ? map.occupiers : Empty;
        }

        /// <summary>整表替换某地图的势力（传 null = 清空），并落盘。</summary>
        public static void Set(string mapId, List<OccupierEntry> occupiers)
        {
            if (string.IsNullOrEmpty(mapId)) return;
            EnsureLoaded();
            var map = FindMap(mapId);
            if (map == null)
            {
                map = new OccupierMapData { mapId = mapId };
                _data.maps.Add(map);
            }
            map.occupiers = occupiers ?? new List<OccupierEntry>();
            Save();
        }

        /// <summary>磁盘完整路径（与存档同级）。</summary>
        public static string FilePath()
        {
            return ArchivesDataBase_SO.DeletePath() + FileName;
        }

        /// <summary>从磁盘重新读（覆盖内存）。⚠ 文件是外部可改的输入 ⇒ 坏了按空表处理并报错。</summary>
        public static void Load()
        {
            string path = FilePath();

            if (!File.Exists(path))
            {
                var template = Resources.Load<TextAsset>(TemplatePath);
                if (template == null)
                {
                    _data = new OccupierStateData();
                    Debug.LogError($"[势力] 缺少初始模板 Resources/{TemplatePath}，按空表启动（每个地图都不会显示势力）。");
                    return;
                }
                _data = Parse(template.text) ?? new OccupierStateData();
                Debug.Log($"[势力] 首次启动，已用模板 {TemplatePath} 初始化 {_data.maps.Count} 个地图");
                Save();   // 落盘一次 ⇒ 之后就以磁盘文件为准（模板只是初始值）
                return;
            }

            try
            {
                _data = Parse(File.ReadAllText(path, Encoding.UTF8)) ?? new OccupierStateData();
            }
            catch (Exception e)
            {
                _data = new OccupierStateData();
                Debug.LogError($"[势力] 读 {path} 失败，按空表启动：{e.Message}");
                return;
            }
            Debug.Log($"[势力] 已加载 {path}（{_data.maps.Count} 个地图）");
        }

        /// <summary>写回磁盘（内存为空时不动，避免把"还没加载"写成空表覆盖掉真数据）。</summary>
        public static void Save()
        {
            if (_data == null) return;
            string path = FilePath();
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(_data, true), new UTF8Encoding(true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[势力] 写 {path} 失败：{e.Message}");
            }
        }

        /// <summary>【联机·发送】整份势力表的 JSON（与磁盘文件同一格式，可直接下发）。</summary>
        public static string ToJson()
        {
            EnsureLoaded();
            return JsonUtility.ToJson(_data);
        }

        /// <summary>【联机·接收】用房主下发的 JSON **整份替换**本地表并落盘。
        /// 空/非法载荷一律丢弃并报错 —— 绝不能用它把本地数据清空。</summary>
        public static void ApplyJson(string json)
        {
            var incoming = Parse(json);
            if (incoming == null)
            {
                Debug.LogError("[势力] 收到的势力表为空或非法，已忽略（本地数据保持不变）");
                return;
            }
            _data = incoming;
            Save();
            Debug.Log($"[势力] 已应用下发的势力表（{_data.maps.Count} 个地图）");
        }

        private static void EnsureLoaded()
        {
            if (_data == null) Load();
        }

        private static OccupierMapData FindMap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId) || _data == null) return null;
            return _data.maps.Find(m => m != null && m.mapId == mapId);
        }

        private static OccupierStateData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var data = JsonUtility.FromJson<OccupierStateData>(json);
                if (data != null && data.maps == null) data.maps = new List<OccupierMapData>();
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[势力] JSON 解析失败：{e.Message}");
                return null;
            }
        }
    }
}
