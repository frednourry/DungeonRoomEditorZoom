using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class RoomNodeSO : ScriptableObject {
    [HideInInspector] public string id;
    [HideInInspector] public List<string> parentRoomNodeIDList = new List<string>();    // We're only use 1 parent for this game
    [HideInInspector] public List<string> childRoomNodeIDList = new List<string>();
    [HideInInspector] public RoomNodeGraphSO roomNodeGraph;
    public RoomNodeTypeSO roomNodeType;
    [HideInInspector] public RoomNodeTypeListSO roomNodeTypeList;

    #region Editor Code
#if UNITY_EDITOR

    [HideInInspector] public Rect rect;
    [HideInInspector] public Rect rectZoomed;                                       // ADDED FOR ZOOM (will be updated in the new Draw(...))
    [HideInInspector] public bool isLeftClickDragging = false;
    [HideInInspector] public bool isSelected = false;

    public void Initialise(Rect rect, RoomNodeGraphSO nodeGraph, RoomNodeTypeSO roomNodeType) {
        this.rect = rect;
        this.id = Guid.NewGuid().ToString();
        this.name = "RoomNode";
        this.roomNodeGraph = nodeGraph;
        this.roomNodeType = roomNodeType;

        // Load room node type list
        roomNodeTypeList = GameResources.Instance.roomNodeTypeList;
    }


    public void Draw(GUIStyle roomNodeStyle, Vector2 offset) {                      // ADDED FOR ZOOM
        rectZoomed.Set(rect.x+ offset.x, rect.y+offset.y, rect.width, rect.height);

        // Draw Node Box Using Begin Area
        GUILayout.BeginArea(rectZoomed, roomNodeStyle);                             // MODIFIED FOR ZOOM (use 'rectZoomed' instead of 'rect')

        // Start Region to detect popup selection changes
        EditorGUI.BeginChangeCheck();
        if (parentRoomNodeIDList.Count > 0 || roomNodeType.isEntrance) {
            // Display a label that can't be changed
            EditorGUILayout.LabelField(roomNodeType.roomNodeTypeName);
        } else {
            // Display a popup using the RoomNodeType name values that can be selected from (default to the currently set roomNodeType)
            int selected = roomNodeTypeList.list.FindIndex(x => x == roomNodeType);
            int selection = EditorGUILayout.Popup("", selected, GetRoomNodeTypesToDisplay());

            roomNodeType = roomNodeTypeList.list[selection];

            // If the room type selection has changed making child connections potentially invalid
            if (roomNodeTypeList.list[selected].isCorridor && !roomNodeTypeList.list[selection].isCorridor ||
               !roomNodeTypeList.list[selected].isCorridor && roomNodeTypeList.list[selection].isBossRoom ||
               !roomNodeTypeList.list[selected].isBossRoom && roomNodeTypeList.list[selection].isCorridor) {
                if (childRoomNodeIDList.Count > 0) {
                    for (int i = childRoomNodeIDList.Count - 1; i >= 0; i--) {
                        RoomNodeSO childRoomNode = roomNodeGraph.GetRoomNode(childRoomNodeIDList[i]);

                        if (childRoomNode != null) {
                            RemoveChildRoomNodeIDFromRoomNode(childRoomNode.id);
                            childRoomNode.RemoveParentRoomNodeIDFromRoomNode(id);
                        }
                    }
                }
            }
        }

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(this);

        GUILayout.EndArea();
    }

    public void Draw(GUIStyle roomNodeStyle) {
        // Draw Node Box Using Begin Area
        GUILayout.BeginArea(rect, roomNodeStyle);

        // Start Region to detect popup selection changes
        EditorGUI.BeginChangeCheck();
        if (parentRoomNodeIDList.Count > 0 || roomNodeType.isEntrance) {
            // Display a label that can't be changed
            EditorGUILayout.LabelField(roomNodeType.roomNodeTypeName);
        } else {
            // Display a popup using the RoomNodeType name values that can be selected from (default to the currently set roomNodeType)
            int selected = roomNodeTypeList.list.FindIndex(x => x == roomNodeType);
            int selection = EditorGUILayout.Popup("", selected, GetRoomNodeTypesToDisplay());

            roomNodeType = roomNodeTypeList.list[selection];

            // If the room type selection has changed making child connections potentially invalid
            if (roomNodeTypeList.list[selected].isCorridor && !roomNodeTypeList.list[selection].isCorridor ||
               !roomNodeTypeList.list[selected].isCorridor && roomNodeTypeList.list[selection].isBossRoom ||
               !roomNodeTypeList.list[selected].isBossRoom && roomNodeTypeList.list[selection].isCorridor) {
                if (childRoomNodeIDList.Count > 0) {
                    for (int i = childRoomNodeIDList.Count - 1; i >= 0; i--) {
                        RoomNodeSO childRoomNode = roomNodeGraph.GetRoomNode(childRoomNodeIDList[i]);

                        if (childRoomNode != null) {
                            RemoveChildRoomNodeIDFromRoomNode(childRoomNode.id);
                            childRoomNode.RemoveParentRoomNodeIDFromRoomNode(id);
                        }
                    }
                }
            }
        }

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(this);

        GUILayout.EndArea();
    }


    /// <summary>
    /// Populate a string array wih the room node types to display what can be selected
    /// </summary>
    private string[] GetRoomNodeTypesToDisplay() {
        string[] roomArray = new string[roomNodeTypeList.list.Count];
        for (int i = 0; i< roomNodeTypeList.list.Count; i++) {
            if (roomNodeTypeList.list[i].displayInNodeGraphEditor) {
                roomArray[i] = roomNodeTypeList.list[i].roomNodeTypeName;
            }
        }
        return roomArray;
    }

    /// <summary>
    /// Process events for the node
    /// </summary>
    public void ProcessEvents(Event currentEvent, Vector2 zoomMousePosition, float zoom) {          // MODIFIED FOR ZOOM
        switch (currentEvent.type) { 
            case EventType.MouseDown:
                ProcessMouseDownEvent(currentEvent, zoomMousePosition);
                break;
            case EventType.MouseUp:
                ProcessMouseUpEvent(currentEvent);
                break;
            case EventType.MouseDrag:
                ProcessMouseDragEvent(currentEvent, zoom);
                break;
            default: 
                break;
        }
    }

    private void ProcessMouseDownEvent(Event currentEvent, Vector2 zoomMousePosition) {             // MODIFIED FOR ZOOM
        if (currentEvent.button == 0) {
            ProcessLeftClickDownEvent();
        } else if (currentEvent.button == 1) {
            ProcessRightClickDownEvent(currentEvent, zoomMousePosition);
        }
    }

    private void ProcessLeftClickDownEvent() {
        Selection.activeObject = this;  // To select this node in the Project>Assets window

        isSelected = !isSelected;
    }

    private void ProcessRightClickDownEvent(Event currentEvent, Vector2 zoomMousePosition) {        // MODIFIED FOR ZOOM
      roomNodeGraph.SetNodeToDrawConnectionLineFrom(this, zoomMousePosition);       
    }

    private void ProcessMouseUpEvent(Event currentEvent) {
        if (currentEvent.button == 0) {
            ProcessLeftClickUpEvent();
        }
    }

    private void ProcessLeftClickUpEvent() {
        if (isLeftClickDragging) isLeftClickDragging = false;
    }

    private void ProcessMouseDragEvent(Event currentEvent, float zoom) {                            // MODIFIED FOR ZOOM
        if (currentEvent.button == 0) {
            ProcessLeftClickDragEvent(currentEvent, zoom);
        }
    }

    private void ProcessLeftClickDragEvent(Event currentEvent, float zoom) {                        // MODIFIED FOR ZOOM
        isLeftClickDragging = true;
        DragNode(currentEvent.delta / zoom);
        GUI.changed = true;
    }

    public void DragNode(Vector2 delta) {
        rect.position += delta;
        EditorUtility.SetDirty(this);
    }

    public bool AddChildNodeIDToRoomNode(string childID) {
        if (IsChildRoomValid(childID)) {
            childRoomNodeIDList.Add(childID);
            return true;
        }
        return false;
    }

    public bool AddParentNodeIDToRoomNode(string parentID) {
        parentRoomNodeIDList.Add(parentID);
        return true;
    }

    /// <summary>
    /// Remove childID from the node (return true if the node has been removed)
    /// </summary>
    public bool RemoveChildRoomNodeIDFromRoomNode(string childID) {

        if (childRoomNodeIDList.Contains(childID)) {
            childRoomNodeIDList.Remove(childID);
            return true;
        }

        return true;
    }

    /// <summary>
    /// Remove parentID from the node (return true if the node has been removed)
    /// </summary>
    public bool RemoveParentRoomNodeIDFromRoomNode(string parentID) {

        if (parentRoomNodeIDList.Contains(parentID)) {
            parentRoomNodeIDList.Remove(parentID);
            return true;
        }

        return true;
    }

    private bool IsChildRoomValid(string childID) {
        RoomNodeSO childRoomNode = roomNodeGraph.GetRoomNode(childID);
        if (childRoomNode == null)
            return true;

        RoomNodeTypeSO childType = childRoomNode.roomNodeType;

        // If the node is not connected to itself
        if (id == childID)
            return false;

        // Check if the child node has a type of none
        if (childType.isNone)
            return false;

        // Check is there is already a connected boss room
        bool isConnectedBossNodeAlready = false;
        foreach (RoomNodeSO roomNode in roomNodeGraph.roomNodeList) {
            if (roomNode.roomNodeType.isBossRoom && roomNode.parentRoomNodeIDList.Count > 0) {
                isConnectedBossNodeAlready = true;
            }
        }

        // Check the child node has a type of boss room and there is already a connected boss room node, then return false
        if (childType.isBossRoom && isConnectedBossNodeAlready) {
            return false;
        }

        // If the node already has a child with this child ID
        if (childRoomNodeIDList.Contains(childID))
            return false;

        // If this childID is already in the parentID list
        if (parentRoomNodeIDList.Contains(childID))
            return false;

        // If the child node already has a parent
        if (childRoomNode.parentRoomNodeIDList.Count > 0)
            return false;

        // If the nodes are not corridors
        if (childType.isCorridor && roomNodeType.isCorridor)
            return false;

        // If the nodes are not rooms
        if (!childType.isCorridor && !roomNodeType.isCorridor)
            return false;

        // If we don't exceed the maximum corridors allowed
        if (childType.isCorridor && childRoomNodeIDList.Count >= Settings.maxChildCorridors)
            return false;

        if (childType.isEntrance)
            return false;

        // If adding a room to a corridor, check that this corridor node doesn't already have a room added
        if (!childType.isCorridor && childRoomNodeIDList.Count > 0)
            return false;


        return true;
    }

#endif
    #endregion Editor Code
}
