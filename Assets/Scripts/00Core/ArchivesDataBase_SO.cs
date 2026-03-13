using System;
using System.IO;
using System.Text;
using Unity.BaseTool;
using UnityEngine;

namespace BaseLibrary
{
    public abstract class ArchivesDataBase_SO : ScriptableObject
    {
        public static string teststring;


        public abstract string Path();


        [Header("用户")]
        [CustomLabel("名称")]
        public string playerName;
        [CustomLabel("UID")]
        public int UID;

        #region IO

        public static string DeletePath()
        {
#if UNITY_ANDROID
        string DeletePath = Application.persistentDataPath + "/";
#elif UNITY_STANDALONE_WIN
            string DeletePath = Application.dataPath + "/../";
#if UNITY_EDITOR
            DeletePath += "/../";
#endif
#endif
            return DeletePath;
        }


        //Application.dataPath=asset文件夹，上级两次就是在整个工程同级的位置
        //创建文件夹路径（/../代表Application.dataPath 路径的上一级目录）
        protected void SaveFile()
        {
            string jsonStr = JsonUtility.ToJson(this);
            Debug.LogWarning(jsonStr);
            string deletePath = DeletePath();

            //if (Application.platform == RuntimePlatform.Android){
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
        {
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
        }
#endif

            string DeletePaths = deletePath + Path();
            FileInfo fileInfo = new FileInfo(DeletePaths);

            if (fileInfo.Exists)
            {
                fileInfo.Delete();
            }
            using (FileStream fs = fileInfo.Create())
            {
                Byte[] info = new UTF8Encoding(true).GetBytes(jsonStr);
                fs.Write(info, 0, info.Length);
                File.SetAttributes(DeletePaths, FileAttributes.Normal);//设置隐藏文件
                                                                       //FileAttributes.Normal 设置正常文件
                                                                       //FileAttributes.Hidden 设置隐藏文件
            }
            Debug.LogWarning("存档文件保存到" + DeletePaths);
        }


        public static ArchivesDataBase_SO Load()
        {
            var tmp = Instantiate(Resources.Load<ArchivesDataBase_SO>("GameData/Archive_Default"));
            //string DeletePath = Application.dataPath;
            string deletePath = DeletePath();

            //if (Application.platform == RuntimePlatform.Android){
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
        {
                Permission.RequestUserPermission(Permission.ExternalStorageRead);
            teststring += "获得到权限+\n";
        }
#endif
            string DeletePaths = deletePath + tmp.Path();
            FileInfo fileInfo = new FileInfo(DeletePaths);
            if (fileInfo.Exists)
            {

                Debug.LogWarning(DeletePaths + "找到了存档"+fileInfo.OpenText().ReadToEnd());
                JsonUtility.FromJsonOverwrite(fileInfo.OpenText().ReadToEnd(), tmp);
                teststring += "存档内容" + fileInfo.OpenText().ReadToEnd();
            }
            else
            {
                teststring += DeletePaths + "没有找到存档，在" + deletePath + tmp.Path() + "创建新存档";
                Debug.LogError(DeletePaths + "没有找到存档，在" + deletePath + tmp.Path() + "创建新存档");
                tmp.name = "Archive";
                tmp.NewPlayerLogon();
                tmp.SaveFile();
            }
            tmp.InLoad();
            return tmp;
        }


        protected abstract void InLoad();


        public void NewPlayerLogon()
        {
            if (UID == 0)
            {
                int uid = (int)((DateTime.Now.ToFileTime() / 100) % 10000000);
                UID = uid;
            }
            playerName = "用户"+ UID;
            SaveFile();
        }

        #endregion

        #region 反射
        /*
        protected static PropertyInfo[] propertyS;

        protected static void InitRef()
        {
            var tmp = Resources.Load<ArchivesDataBase_SO>("GameData/Archive_Default");
            propertyS = tmp.GetType().GetProperties();
            
            //foreach (var i in propertyS)
            //{
            //    Debug.LogWarning("名称：" + i.Name + "类型：" + i.PropertyType);
            //}
            //Debug.LogError("初始化属性反射"); 
        }
        public float this[string name]
        {
            get
            {
                var value = propertyS.FirstOrDefault(x => x.Name == name).GetValue(this, null);
                if (value is int) return Convert.ToSingle(value);
                else if (value is bool) return Convert.ToSingle(value);
                return Convert.ToSingle(value);
            }
            set => propertyS.FirstOrDefault(x => x.Name == name).SetValue(this, value, null);
        }
    */
        #endregion


    }
}