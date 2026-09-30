using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 读取csv格式文件
/// </summary>
public class ConfigData
{
    private Dictionary<int, Dictionary<string, string>> datas;//一个配置表的数据，键是字典id，值是一行数据（也是字典）
    public string fileName;//配置表文件名称
    public ConfigData(string fileName)
    {
        this.fileName = fileName;
        this.datas = new Dictionary<int, Dictionary<string, string>>();
    }

    public TextAsset LoadFile()
    {
        return Resources.Load<TextAsset>($"config/{fileName}");
    }

    /*
    public void Load(string txt)
    {
        string[] dataArr = txt.Split("\n");//标题行
        string[] titleArr = dataArr[0].Trim().Split(',');//字典的键
        for (int i = 2; i < dataArr.Length; i++)
        {
            string[] tempArr = dataArr[i].Trim().Split(',');
            if (dataArr[i].Trim() == "")
                continue;
            Dictionary<string, string> tempData = new Dictionary<string, string>();
            for (int j = 0; j < tempArr.Length; j++)
            {
                tempData.Add(titleArr[j], tempArr[j]);
            }
            datas.Add(int.Parse(tempData["Id"]), tempData);
        }
    }
    */
    public void Load(string txt)
    {
        datas.Clear();

        string[] dataArr = txt.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        if (dataArr.Length < 2)
        {
            Debug.LogError($"配置表 {fileName} 内容不足");
            return;
        }

        // 第一行是标题（字段名）
        string[] titleArr = dataArr[0].Trim().Split(',');
        int idIndex = Array.IndexOf(titleArr, "Id");
        if (idIndex == -1)
        {
            Debug.LogError($"配置表 {fileName} 缺少 Id 列！");
            return;
        }

        for (int i = 2; i < dataArr.Length; i++)
        {
            string line = dataArr[i].Trim();

            //如果遇到空行，直接结束读取，防止尾部空行干扰
            if (string.IsNullOrWhiteSpace(line))
            {
                Debug.Log($"⚠️ 配置表 {fileName} 在第 {i + 1} 行检测到空行，停止读取。");
                break;
            }

            // 跳过标题或注释行
            if (line.StartsWith("Id")) continue;

            string[] tempArr = line.Split(',');
            if (tempArr.Length != titleArr.Length)
            {
                Debug.LogWarning($"配置表 {fileName} 第 {i + 1} 行列数不匹配：{line}");
                continue;
            }

            string idStr = tempArr[idIndex].Trim();
            if (!int.TryParse(idStr, out int id))
            {
                Debug.LogWarning($"配置表 {fileName} 第 {i + 1} 行的 Id 无效：{idStr}");
                continue;
            }

            // 构建当前行数据字典
            Dictionary<string, string> tempData = new Dictionary<string, string>();
            for (int j = 0; j < titleArr.Length; j++)
            {
                tempData[titleArr[j].Trim()] = tempArr[j].Trim();
            }

            datas[id] = tempData;
        }
    }

    public Dictionary<string, string> GetDataById(int id)
    {
        if (datas.ContainsKey(id))
            return datas[id];
        return null;
    }
    public Dictionary<int, Dictionary<string, string>> GetLines()
    {
        return datas;
    }
}

public class ConfigController : BaseManager<ConfigController>
{
    private Dictionary<string, ConfigData> configs;//配置表

    private ConfigController()
    {
        configs = new Dictionary<string, ConfigData>();
    }


    public ConfigData GetConfigData(string file)
    {
        if (configs.ContainsKey(file))
            return configs[file];
        else
        {
            ConfigData newConfig = new ConfigData(file);
            TextAsset textAsset = newConfig.LoadFile();
            newConfig.Load(textAsset.text);
            configs.Add(newConfig.fileName, newConfig);
            return configs[file];
        }
    }
}
