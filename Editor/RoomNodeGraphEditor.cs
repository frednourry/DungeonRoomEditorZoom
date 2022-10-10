using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

public class RoomNodeGraphEditor : EditorWindow {
    private static RoomNodeGraphSO currentRoomNodeGraph;

    private Vector2 graphOffset;                            // NO MORE NEEDED FOR ZOOM (see zoomCoordsOrigin)
    private Vector2 graphDrag;                              // NO MORE NEEDED FOR ZOOM (see zoomCoordsOrigin)

    private RoomNodeSO currentRoomNode = null;
    private RoomNodeTypeListSO roomNodeTypeList;

    // Node layout values
    private const float nodeWidth = 160f;
    private const float nodeHeight = 75f;
    private const int nodePadding = 25;
    private const int nodeBorder = 15;

    private const float connectingLineWidth = 3f;
    private const float connectingLineArrowsize = 6f;

    // Grid spacing
    private const float gridLarge = 100f;
    private const float gridSmall = 25f;

    // Node Style
    private Dictionary<string, GUIStyle> styleDictionary = new Dictionary<string, GUIStyle>();
    private RoomNodeTypeSO roomNodeTypeChest;

    // ADDED FOR ZOOM
    private float zoom = 1f;
    private float deltaZoom = 150f;
    private const float zoomMin = 0.25f;
    private const float zoomMax = 2f;
    private Rect zoomArea = new Rect(0.0f, 0.0f, 1092f, 724.0f);
    private Vector2 zoomCoordsOrigin = Vector2.zero;


    [MenuItem("Room Node Graph Editor", menuItem = "Window/Dungeon Editor/Room Node Graph Editor")]

    private static void OpenWindows() { 
        GetWindow<RoomNodeGraphEditor>("Room Node Graph Editor");
    }

    private void OnEnable() {
        // Subscribe to the inspector selection changed event
        Selection.selectionChanged += InspectorSelectionChanged;

        // Define node layout style
        GUIStyle tempStyle, roomNodeStyle = new GUIStyle();
        roomNodeStyle.normal.background = EditorGUIUtility.Load("node1") as Texture2D;
        roomNodeStyle.normal.textColor = Color.white;
        roomNodeStyle.padding = new RectOffset(nodePadding, nodePadding, nodePadding, nodePadding);
        roomNodeStyle.border = new RectOffset(nodeBorder, nodeBorder, nodeBorder, nodeBorder);
        styleDictionary.Add("normal", roomNodeStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node1 on") as Texture2D;
        styleDictionary.Add("normal selected", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node3") as Texture2D;
        styleDictionary.Add("entrance", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node3 on") as Texture2D;
        styleDictionary.Add("entrance selected", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node6") as Texture2D;
        styleDictionary.Add("boss", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node6 on") as Texture2D;
        styleDictionary.Add("boss selected", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node0") as Texture2D;
        styleDictionary.Add("corridor", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node0 on") as Texture2D;
        styleDictionary.Add("corridor selected", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node5") as Texture2D;
        styleDictionary.Add("chest", tempStyle);

        tempStyle = new GUIStyle(roomNodeStyle);
        tempStyle.normal.background = EditorGUIUtility.Load("node5 on") as Texture2D;
        styleDictionary.Add("chest selected", tempStyle);

        // Load Room node types
        roomNodeTypeList = GameResources.Instance.roomNodeTypeList;

        roomNodeTypeChest = roomNodeTypeList.list.Find(x => x.roomNodeTypeName.Contains("Chest"));
    }

    private void OnDisable() {
        // Unsubscribe to the inspector selection changed event
        Selection.selectionChanged -= InspectorSelectionChanged;
    }

    /// <summary>
    /// Selection changed in the editor
    /// </summary>
    private void InspectorSelectionChanged() {
        RoomNodeGraphSO roomNodeGraph = Selection.activeObject as RoomNodeGraphSO;
        if (roomNodeGraph != null) {
            resetZoom();                                                                    // ADDED FOR ZOOM (reset zoom when changing graph)
            currentRoomNodeGraph = roomNodeGraph;
            GUI.changed = true;
        }

    }

    private void resetZoom() {
        zoomCoordsOrigin = Vector2.zero;
        zoom = 1;
    }

    /// <summary>
    /// Open the room node graph editor window if a room node graph scriptable object asset is double clicked in the inspector
    /// </summary>
    [OnOpenAsset(0)] // Need the namescape UnityEditor.Callbacks
    public static bool OnDoubleClickAsset(int instanceID, int line) {
        RoomNodeGraphSO roomNodeGraph = EditorUtility.InstanceIDToObject(instanceID) as RoomNodeGraphSO;
        if (roomNodeGraph != null) {
            OpenWindows();

            currentRoomNodeGraph = roomNodeGraph;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Draw Editor GUI 
    /// </summary>
    private void OnGUI() {

        // If a scriptable object of the RoomNodeGraphSO has been selected, then process
        if (currentRoomNodeGraph != null) {
            // Process events
            ProcessEvents(Event.current);                                                       // MOVED FOR ZOOM (shouldn't be inside the drawing process)

            // DRAWING PROCESS
            zoomArea = new Rect(0 ,0, position.width, position.height);                         // ADDED FOR ZOOM

            // Within the zoom area all coordinates are relative to the top left corner of the zoom area
            // with the width and height being scaled versions of the original/unzoomed area's width and height.
            EditorZoomArea.Begin(zoom, zoomArea);    // ADDED FOR ZOOM

            // ZOOMED AREA

            // Draw grids
            DrawBackgroundGrid(gridSmall, 0.2f, Color.gray);
            DrawBackgroundGrid(gridLarge, 0.3f, Color.gray);

            // Draw lines if being dragged
            DrawDragLine();

            // Draw connection between room nodes
            DrawRoomConnections();

            // Draw Room Nodes
            DrawRoomNodes();

            // END OF THE ZOOMED AREA
            EditorZoomArea.End();   // ADDED FOR ZOOM

            // BELOW, DRAW EVERYTHING WITHOUT ZOOM
            GUI.Box(new Rect(0.0f, position.height-24f, position.width, 24.0f), "ZOOM = " + zoom);
            if (GUI.Button(new Rect(5.0f, position.height - 23f, 100.0f, 20.0f), "Reset ZOOM")) resetZoom();

            // END DRAWING PROCESS
        }

        if (GUI.changed) 
            Repaint();
    }

    private void ProcessEvents(Event currentEvent) {
        // Reset graph drag
        graphDrag = Vector2.zero;                            // MODIFIED FOR ZOOM

        if (currentRoomNode == null || !currentRoomNode.isLeftClickDragging) { 
            if (currentEvent.type == EventType.MouseDown)                           // ADDED FOR ZOOM
                currentRoomNode = IsMouseOverRoomNode(currentEvent);
        }

        if (currentRoomNode == null || currentRoomNodeGraph.roomNodeToDrawLineFrom != null || currentEvent.type == EventType.ScrollWheel) {
            ProcessRoomNodeGraphEvents(currentEvent);
        } else {
            Vector2 zoomMousePosition = (currentEvent.mousePosition - zoomArea.TopLeft()) / zoom;   // MODIFIED FOR ZOOM
            currentRoomNode.ProcessEvents(currentEvent, zoomMousePosition, zoom);
        }

    }

    /// <summary>
    /// Process Room Node Graph Events
    /// </summary>
    private void ProcessRoomNodeGraphEvents(Event currentEvent) {
        switch (currentEvent.type) { 
            case EventType.MouseDown:
                ProcessMouseDownEvent(currentEvent);
                break;

            case EventType.MouseDrag:
                ProcessMouseDragEvent(currentEvent);
                break;

            case EventType.MouseUp:
                ProcessMouseUpEvent(currentEvent);
                break;

            case EventType.ScrollWheel:                                                 // ADDED FOR ZOOM
                ProcessMouseScrollWheel(currentEvent);
                break;

            default:
                break;
        }
    }

    // ADDED FOR ZOOM
    private Vector2 ConvertScreenCoordsToZoomCoords(Vector2 screenCoords) {
        return (screenCoords - zoomArea.TopLeft()) / zoom + zoomCoordsOrigin;
    }

    // ADDED FOR ZOOM
    private void ProcessMouseScrollWheel(Event currentEvent) {
        ClearLineDrag();

        // Allow adjusting the zoom with the mouse wheel as well. In this case, use the mouse coordinates
        // as the zoom center instead of the top left corner of the zoom area. This is achieved by
        // maintaining an origin that is used as offset when drawing any GUI elements in the zoom area.
        Vector2 screenCoordsMousePos = Event.current.mousePosition;
        Vector2 delta = Event.current.delta;
        Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords(screenCoordsMousePos);
        float zoomDelta = -delta.y / deltaZoom;
        float oldZoom = zoom;
        zoom += zoomDelta;
        zoom = Mathf.Clamp(zoom, zoomMin, zoomMax);
        zoomCoordsOrigin += (zoomCoordsMousePos - zoomCoordsOrigin) - (oldZoom / zoom) * (zoomCoordsMousePos - zoomCoordsOrigin);

        GUI.changed = true;
    }

    private void ProcessMouseDownEvent(Event currentEvent) {
        if (currentEvent.button == 1) {
            ShowContextMenu(currentEvent.mousePosition);
        } else if (currentEvent.button == 0) {
            ClearLineDrag();
            ClearAllSelectedRoomNodes();
        } else if (currentEvent.button == 2) {                                  // ADDED FOR ZOOM
            ClearLineDrag();
        }
    }

    private void ProcessMouseDragEvent(Event currentEvent) {
        if (currentEvent.button == 1) {
            ProcessRightMouseDragEvent(currentEvent);
        } else if (currentEvent.button == 0) {
            ProcessLeftMouseDragEvent(currentEvent.delta);
        } else if (currentEvent.button == 2) {                                  // ADDED FOR ZOOM
            ProcessMiddleMouseDragEvent(currentEvent.delta);
        }
    }

    private void ProcessMouseUpEvent(Event currentEvent) {
        if (currentEvent.button == 1 && currentRoomNodeGraph.roomNodeToDrawLineFrom != null) {
            // Check if over a node
            RoomNodeSO roomNode = IsMouseOverRoomNode(currentEvent);
            if (roomNode != null) {
                if (currentRoomNodeGraph.roomNodeToDrawLineFrom.AddChildNodeIDToRoomNode(roomNode.id)) {
                    roomNode.AddParentNodeIDToRoomNode(currentRoomNodeGraph.roomNodeToDrawLineFrom.id);
                }
            }

            ClearLineDrag();
        }
    }

    private void ProcessLeftMouseDragEvent(Vector2 dragDelta) {
        graphDrag = dragDelta / zoom;                                           // MODIFIED FOR ZOOM
        for (int i = 0; i<currentRoomNodeGraph.roomNodeList.Count; i++) {
            currentRoomNodeGraph.roomNodeList[i].DragNode(graphDrag);     
        }
        GUI.changed = true;
    }

    // ADDED FOR ZOOM
    private void ProcessMiddleMouseDragEvent(Vector2 dragDelta) {
        zoomCoordsOrigin -= dragDelta / zoom;
        GUI.changed = true;
    }

    private void ProcessRightMouseDragEvent(Event currentEvent) {
        if (currentRoomNodeGraph.roomNodeToDrawLineFrom != null) {
            DragConnectingLine(currentEvent.delta/zoom);                // MODIFIED FOR ZOOM
            GUI.changed = true;
        }
    }

    public void DragConnectingLine(Vector2 delta) {
        currentRoomNodeGraph.linePosition += delta;
    }

    private RoomNodeSO IsMouseOverRoomNode(Event currentEvent) {
        Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords(currentEvent.mousePosition);   // ADDED FOR ZOOM (to use instead of 'currentEvent.mousePosition')

        for (int i = currentRoomNodeGraph.roomNodeList.Count - 1; i >= 0; i--) {
            if (currentRoomNodeGraph.roomNodeList[i].rect.Contains(zoomCoordsMousePos)) {
                    return currentRoomNodeGraph.roomNodeList[i];
            }
        }

        return null;
    }

    private void ShowContextMenu(Vector2 mousePosition) {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Create Room Node"), false, CreateRoomNode, mousePosition);
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Select All Room Nodes"), false, SelectAllRoomNodes);
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete Selected Room Node Links"), false, DeleteSelectedRoomNodeLinks);
        menu.AddItem(new GUIContent("Delete Selected Room Nodes"), false, DeleteSelectedRoomNode);

        menu.ShowAsContext();
    }

    private void SelectAllRoomNodes() {
        foreach (RoomNodeSO roomNode in currentRoomNodeGraph.roomNodeList) {
            roomNode.isSelected = true;
        }
        GUI.changed = true;
    }

    private void CreateRoomNode(object mousePositionObject) {
        if (currentRoomNodeGraph.roomNodeList.Count == 0) {
            Vector2 zoomDefaultPos = ConvertScreenCoordsToZoomCoords(new Vector2(200f, 200f));          // ADD FOR ZOOM
            CreateRoomNode(zoomDefaultPos, roomNodeTypeList.list.Find(x=>x.isEntrance)) ;
        }

        Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords((Vector2)mousePositionObject);     // ADD FOR ZOOM
        CreateRoomNode(zoomCoordsMousePos, roomNodeTypeList.list.Find(x =>x.isNone));
    }


    private void CreateRoomNode(object mousePositionObject, RoomNodeTypeSO roomNodeType) {
        Vector2 mousePosition = (Vector2)mousePositionObject;

        // Create room node scriptable object asset
        RoomNodeSO roomNode = ScriptableObject.CreateInstance<RoomNodeSO>();

        // add room node to current room node graph room node list
        currentRoomNodeGraph.roomNodeList.Add(roomNode);

        // Set room node values
        roomNode.Initialise(new Rect(mousePosition, new Vector2(nodeWidth, nodeHeight)), currentRoomNodeGraph, roomNodeType);

        // Add room node to room node graph scriptable object asset database
        AssetDatabase.AddObjectToAsset(roomNode, currentRoomNodeGraph);

        AssetDatabase.SaveAssets();

        // Refresh graph node dictionary
        currentRoomNodeGraph.OnValidate();
    }

    /// <summary>
    /// Delete the links between the selected room nodes
    /// </summary>
    private void DeleteSelectedRoomNodeLinks() {
        foreach (RoomNodeSO roomNode in currentRoomNodeGraph.roomNodeList) {
            if (roomNode.isSelected && roomNode.childRoomNodeIDList.Count > 0) {
                for (int i = roomNode.childRoomNodeIDList.Count - 1; i >= 0; i--) {
                    RoomNodeSO childRoomNode = currentRoomNodeGraph.GetRoomNode(roomNode.childRoomNodeIDList[i]);

                    if (childRoomNode != null && childRoomNode.isSelected) {
                        roomNode.RemoveChildRoomNodeIDFromRoomNode(childRoomNode.id);
                        childRoomNode.RemoveParentRoomNodeIDFromRoomNode(roomNode.id);
                    }
                }
            }
        }

        ClearAllSelectedRoomNodes();
    }

    /// <summary>
    /// Delete selected room nodes
    /// </summary>
    private void DeleteSelectedRoomNode() {
        Queue<RoomNodeSO> roomNodeDeletionQueue = new Queue<RoomNodeSO>();

        foreach (RoomNodeSO roomNode in currentRoomNodeGraph.roomNodeList) {
            if (roomNode.isSelected && !roomNode.roomNodeType.isEntrance) {
                roomNodeDeletionQueue.Enqueue(roomNode);

                // Iterate through child room nodes ids
                foreach (string childID in roomNode.childRoomNodeIDList) {
                    RoomNodeSO childRoomNode = currentRoomNodeGraph.GetRoomNode(childID);
                    if (childRoomNode != null) {
                        childRoomNode.RemoveParentRoomNodeIDFromRoomNode(roomNode.id);
                    }
                }
                foreach (string parentID in roomNode.parentRoomNodeIDList) {
                    RoomNodeSO parentRoomNode = currentRoomNodeGraph.GetRoomNode(parentID);
                    if (parentRoomNode != null) {
                        parentRoomNode.RemoveChildRoomNodeIDFromRoomNode(roomNode.id);
                    }
                }
            }
        }

        while (roomNodeDeletionQueue.Count > 0) { 
            RoomNodeSO roomNodeToDelete = roomNodeDeletionQueue.Dequeue();

            currentRoomNodeGraph.roomNodeDictionary.Remove(roomNodeToDelete.id);

            currentRoomNodeGraph.roomNodeList.Remove(roomNodeToDelete);

            DestroyImmediate(roomNodeToDelete, true);   // True to delete the assets
        }

        // Save assets database
        AssetDatabase.SaveAssets();
    }

    private void DrawRoomNodes() {
        // Loop through all room nodes and draw them
        foreach (RoomNodeSO roomNode in currentRoomNodeGraph.roomNodeList) {
            string styleName;
            GUIStyle style;

            roomNodeTypeList = GameResources.Instance.roomNodeTypeList;
            if (roomNode.roomNodeType == roomNodeTypeChest) {
                styleName = "chest";
            } else if (roomNode.roomNodeType.isEntrance)
                styleName = "entrance";
            else if (roomNode.roomNodeType.isBossRoom)
                styleName = "boss";
            else if (roomNode.roomNodeType.isCorridor)
                styleName = "corridor";
            else
                styleName = "normal";

            if (roomNode.isSelected) {
                styleName += " selected";
            } 

            style = styleDictionary[styleName];

            roomNode.Draw(style, -zoomCoordsOrigin);                                                            // MODIFIED FOR ZOOM
        }

        GUI.changed = true;
    }

    private void DrawBackgroundGrid(float gridSize, float gridOpacity, Color gridColor) {
        int verticalLineCount = Mathf.CeilToInt((position.width / zoom + gridSize) / gridSize);                 // MODIFIED FOR ZOOM
        int horizontalLineCount = Mathf.CeilToInt((position.height / zoom + gridSize) / gridSize);              // MODIFIED FOR ZOOM

        Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

//        graphOffset += graphDrag * 0.5f;                                                                      // MODIFIED FOR ZOOM

        Vector3 gridOffset = new Vector3(-zoomCoordsOrigin.x % gridSize,  -zoomCoordsOrigin.y % gridSize, 0);   // MODIFIED FOR ZOOM

        for (int i = 0; i<verticalLineCount; i++) {
            Handles.DrawLine(new Vector3(gridSize * i, -gridSize, 0) + gridOffset,
                             new Vector3(gridSize * i, position.height/zoom + gridSize, 0) + gridOffset);       // MODIFIED FOR ZOOM
        }

        for (int j = 0; j < horizontalLineCount; j++) {
            Handles.DrawLine(new Vector3(-gridSize, gridSize * j, 0) + gridOffset,
                             new Vector3(position.width / zoom + gridSize, gridSize * j, 0) + gridOffset);      // MODIFIED FOR ZOOM
    }

    Handles.color = Color.white;
    }

    private void DrawDragLine() {
        if (currentRoomNodeGraph.linePosition != Vector2.zero) {
            Handles.DrawBezier(currentRoomNodeGraph.roomNodeToDrawLineFrom.rectZoomed.center, currentRoomNodeGraph.linePosition,
                currentRoomNodeGraph.roomNodeToDrawLineFrom.rectZoomed.center, currentRoomNodeGraph.linePosition, Color.white, null, connectingLineWidth);
        }
    }
    private void ClearLineDrag() {
        currentRoomNodeGraph.roomNodeToDrawLineFrom = null;
        currentRoomNodeGraph.linePosition = Vector2.zero;
        GUI.changed = true;
    }


    private void DrawConnectionLine(RoomNodeSO parentRoomNodeSO, RoomNodeSO childRoomNodeSO) {
        Vector2 startPosition = parentRoomNodeSO.rectZoomed.center;           // MODIFIED FOR ZOOM
        Vector2 endPosition = childRoomNodeSO.rectZoomed.center;              // MODIFIED FOR ZOOM

        Vector2 midPosition = (endPosition + startPosition) / 2f;
        Vector2 direction = endPosition - startPosition;

        Vector2 arrowTailPoint1 = midPosition - new Vector2(-direction.y, direction.x).normalized * connectingLineArrowsize;
        Vector2 arrowTailPoint2 = midPosition + new Vector2(-direction.y, direction.x).normalized * connectingLineArrowsize;

        Vector2 arrowHeadPoint = midPosition + direction.normalized * connectingLineArrowsize;


        Handles.DrawBezier(startPosition, endPosition, startPosition, endPosition, Color.white, null, connectingLineWidth);

        Handles.DrawBezier(arrowHeadPoint, arrowTailPoint1, arrowHeadPoint, arrowTailPoint1, Color.white, null, connectingLineWidth);
        Handles.DrawBezier(arrowHeadPoint, arrowTailPoint2, arrowHeadPoint, arrowTailPoint2, Color.white, null, connectingLineWidth);

        GUI.changed = true;
    }

    private void DrawRoomConnections() {
        foreach (RoomNodeSO roomNode in currentRoomNodeGraph.roomNodeList) {
            if (roomNode.childRoomNodeIDList.Count > 0) {

                foreach (string childRoomNodeID in roomNode.childRoomNodeIDList) {
                    if (currentRoomNodeGraph.roomNodeDictionary.ContainsKey(childRoomNodeID)) {
                        DrawConnectionLine(roomNode, currentRoomNodeGraph.roomNodeDictionary[childRoomNodeID]);

                        GUI.changed = true;
                    }
                }
            }
        }
    }

    private void ClearAllSelectedRoomNodes() {
        foreach (RoomNodeSO roomNode in currentRoomNodeGraph.roomNodeList) {
            if (roomNode.isSelected) {
                roomNode.isSelected = false;
                GUI.changed = true;
            }
        }
    }
}


// Helper Rect extension methods
public static class RectExtensions {
    public static Vector2 TopLeft(this Rect rect) {
        return new Vector2(rect.xMin, rect.yMin);
    }

    public static Rect ScaleSizeBy(this Rect rect, float scale) {
        return rect.ScaleSizeBy(scale, rect.center);
    }
    public static Rect ScaleSizeBy(this Rect rect, float scale, Vector2 pivotPoint) {
        Rect result = rect;
        result.x -= pivotPoint.x;
        result.y -= pivotPoint.y;
        result.xMin *= scale;
        result.xMax *= scale;
        result.yMin *= scale;
        result.yMax *= scale;
        result.x += pivotPoint.x;
        result.y += pivotPoint.y;
        return result;
    }
    /*    public static Rect ScaleSizeBy(this Rect rect, Vector2 scale) {
            return rect.ScaleSizeBy(scale, rect.center);
        }
        public static Rect ScaleSizeBy(this Rect rect, Vector2 scale, Vector2 pivotPoint) {
            Rect result = rect;
            result.x -= pivotPoint.x;
            result.y -= pivotPoint.y;
            result.xMin *= scale.x;
            result.xMax *= scale.x;
            result.yMin *= scale.y;
            result.yMax *= scale.y;
            result.x += pivotPoint.x;
            result.y += pivotPoint.y;
            return result;
        }*/
}

public class EditorZoomArea {
    private const float kEditorWindowTabHeight = 21.0f;
    private static Matrix4x4 _prevGuiMatrix;

    public static Rect Begin(float zoomScale, Rect screenCoordsArea) {
        GUI.EndGroup();        // End the group Unity begins automatically for an EditorWindow to clip out the window tab. This allows us to draw outside of the size of the EditorWindow.

        Rect clippedArea = screenCoordsArea.ScaleSizeBy(1.0f / zoomScale, screenCoordsArea.TopLeft());
        clippedArea.y += kEditorWindowTabHeight;
        GUI.BeginGroup(clippedArea);

        _prevGuiMatrix = GUI.matrix;
        Matrix4x4 translation = Matrix4x4.TRS(clippedArea.TopLeft(), Quaternion.identity, Vector3.one);
        Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoomScale, zoomScale, 1.0f));
        GUI.matrix = translation * scale * translation.inverse * GUI.matrix;

        return clippedArea;
    }

    public static void End() {
        GUI.matrix = _prevGuiMatrix;
        GUI.EndGroup();
        GUI.BeginGroup(new Rect(0.0f, kEditorWindowTabHeight, Screen.width, Screen.height));
    }
}
