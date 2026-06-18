using UnityEngine;
using UnityEngine.UI;

public class GearManager : MonoBehaviour
{
    public GameObject GearOnOffUI;
    public RectTransform NoteContainer;
    public RectTransform GearContainer; 

    public GameObject SpawnGearUI(Vector2 localPos, bool isOn)
    {
        // NoteContainer 대신 GearContainer 아래 생성
        GameObject effect = Instantiate(GearOnOffUI, GearContainer);
        RectTransform rt = effect.GetComponent<RectTransform>();

        // [핵심] NoteContainer 기준 좌표를 GearContainer 기준으로 변환
        Vector3 worldPos = NoteContainer.TransformPoint(new Vector3(localPos.x, localPos.y, 0));
        rt.position = worldPos;

        Image iconImage = effect.GetComponent<Image>();
        if (iconImage == null) iconImage = effect.GetComponentInChildren<Image>();
        if (iconImage != null)
            iconImage.color = isOn ? Color.green : Color.red;

        bool isLeft = localPos.x < 0;
        rt.localRotation = isLeft ? Quaternion.identity : Quaternion.Euler(0, 0, 180);

        return effect;
    }
}

