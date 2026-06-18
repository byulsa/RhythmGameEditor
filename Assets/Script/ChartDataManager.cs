using UnityEngine;
using System.IO;

public class ChartDataManager : MonoBehaviour
{
    // 저장 경로 반환 (Assets/StreamingAssets/파일명.json)
    private string GetPath(string fileName) 
    {
        string dir = Application.streamingAssetsPath;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName + ".json");
    }

    // 데이터를 JSON으로 변환하여 저장
    public void SaveChart(string fileName, SaveData data)
    {
        // true 설정 시 사람이 읽기 편하게 들여쓰기(Pretty Print)가 적용됩니다.
        string json = JsonUtility.ToJson(data, true); 
        File.WriteAllText(GetPath(fileName), json);
        Debug.Log($"<color=green>[Save Success]</color> 경로: {GetPath(fileName)}");
    }

    // JSON 파일을 읽어 데이터 객체로 변환
    public SaveData LoadChart(string fileName)
    {
        string path = GetPath(fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[Load Fail] 파일을 찾을 수 없습니다: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<SaveData>(json);
    }
}