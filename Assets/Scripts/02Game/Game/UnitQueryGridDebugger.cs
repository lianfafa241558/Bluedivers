using PEMaths;
using UnityEngine;

public class UnitQueryGridDebugger : MonoBehaviour {
    public UnitQueryGrid grid;

    private void OnEnable()
    {
        
    }
#if UNITY_EDITOR

    private void OnDrawGizmos() {
        if (enabled && grid!=null) {
            Gizmos.color = Color.red;
            DrawRect(grid.rect);
            if (grid != null) {
                DrawNode(grid);
            }
        }
        

    }
    
    private void DrawNode(UnitQueryGrid grid) {

        if (grid == null) return;

        Gizmos.color = Color.green;
        foreach (var node in grid.nodes) {
            DrawRect(node.rect);
            Vector3 center = new Vector3((node.rect.x.RawFloat + node.rect.xMax.RawFloat) / 2, 0, (node.rect.y.RawFloat + node.rect.yMax.RawFloat) / 2);
            GUIStyle style = new GUIStyle();
            style.fontSize = 15;
            style.normal.textColor = Color.white;
            //style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            int count = 0;
            foreach (var item in node.units) {
                count += item.Value.Count;
            }
            UnityEditor.Handles.Label(center + new Vector3(0, 0, 1), "数量:" + count.ToString(), style);
            UnityEditor.Handles.Label(center - new Vector3(0, 0, 1), $"坐标({node.x},{node.y})", style);
        }

    }

    private void DrawRect(PERect rect) {
        Vector3 topLeft = new Vector3(rect.x.RawFloat, 0, rect.yMax.RawFloat);
        Vector3 topRight = new Vector3(rect.xMax.RawFloat, 0, rect.yMax.RawFloat);
        Vector3 bottomLeft = new Vector3(rect.x.RawFloat, 0, rect.y.RawFloat);
        Vector3 bottomRight = new Vector3(rect.xMax.RawFloat, 0, rect.y.RawFloat);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        //Gizmos.DrawLine(bottomRight, bottomLeft);
        //Gizmos.DrawLine(bottomLeft, topLeft);
    }
#endif
}
