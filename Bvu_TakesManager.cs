using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BeenoculusVRUtilities
{
    public class Bvu_TakesManager : MonoBehaviour
    {
        #region<--- VARIABLES --->

        public static Bvu_TakesManager Instance;

        public List<Bvu_Take> takes;
        public Bvu_Take currentTake;
        private int currentTakeIndex;
        private bool processingDelay;

        #endregion

        #region<---- MONOBEHAVIOURS ---->

        private void Awake()
        {
            if (Instance != null)
                Destroy(Instance.gameObject);
            Instance = this;
        }

        private void Start()
        {
            if (takes.Count <= 0)
            {
                Debug.LogWarning("No takes found, disabling...");
                gameObject.SetActive(false);
                return;
            }

            StartTake(takes.FirstOrDefault());
        }

        private void Update()
        {
            if (processingDelay)
                return;

            if (currentTake == null)
            {
                Debug.LogError("Current take is null, disabling...");
                gameObject.SetActive(false);
                return;
            }

            currentTake.OnTakeUpdate();

            if (currentTake.IsFinished)
                FinishCurrentTake();
        }

        #endregion

        #region<---- START TAKE METHODS ---->

        private void StartTake(Bvu_Take takeToStart)
        {
            if (takeToStart == null)
            {
                Debug.LogError("Current beeTake is null, disabling...");
                gameObject.SetActive(false);
                return;
            }

            currentTake = takeToStart;

            if (currentTake.startDelayInSecs <= 0)
                ProcessStartTake(takeToStart);
            else
                StartCoroutine(ProcessDelay(currentTake.startDelayInSecs, () => { ProcessStartTake(takeToStart); }));
        }

        private void ProcessStartTake(Bvu_Take takeToStart)
        {
            currentTake = takeToStart;
            currentTake.OnTakeStart();
            currentTakeIndex = takes.IndexOf(currentTake);
        }

        #endregion

        #region<---- FINISH TAKE METHODS ---->

        public void SkipCurrentTake()
        {
            currentTake?.FinishTake();
        }

        private void FinishCurrentTake()
        {
            StopAllCoroutines();

            if (currentTake.finishDelayInSecs <= 0)
                ProcessFinishTake();
            else
                StartCoroutine(ProcessDelay(currentTake.finishDelayInSecs, ProcessFinishTake));
        }

        private void ProcessFinishTake()
        {
            currentTake.OnTakeFinish();

            if (currentTakeIndex + 1 >= takes.Count)
            {
                enabled = false;
                return;
            }

            currentTakeIndex++;
            StartTake(takes[currentTakeIndex]);
        }

        #endregion

        #region<---- LOGICS METHODS ---->

        private IEnumerator ProcessDelay(float delayInSecs, Action onDelayFinish)
        {
            processingDelay = true;
            yield return new WaitForSeconds(delayInSecs);
            processingDelay = false;

            onDelayFinish?.Invoke();
        }

        #endregion
    }

    #region<---- EDITOR ---->

    #if UNITY_EDITOR

    [CustomEditor(typeof(Bvu_TakesManager))]
    public class BeeTakesManagerEditor : Editor
    {
        #region<--- VARIABLES AND PROPERTIES --->

        private SerializedProperty takes;
        private SerializedProperty currentTake;
        private SerializedObject me;

        private bool showBeeTake;
        private int currentTab;
        private int beeTakeIndexToShow = -1;

        private Color unselectedGUIColor;
        private Color selectedGUIColor;
        private Color defaultGUIColor;

        private GUIStyle foldoutStyle;
        private GUIStyle unselectedFoldoutStyle;
        private GUISkin deleteButtonSkin;

        #endregion

        #region<---- EDITOR METHODS ---->

        private void OnEnable()
        {
            me = new SerializedObject(target);

            SetStyles();

            takes = me.FindProperty("takes");
            currentTake = me.FindProperty("currentTake");
        }

        public override void OnInspectorGUI()
        {
            me.Update();
            if (foldoutStyle == null)
            {
                EditorApplication.delayCall += () =>
                {
                    SetStyles();
                    Repaint();
                };
                return;
            }

            EditorGUILayout.Space();

            currentTab = GUILayout.Toolbar(currentTab, new [] { "Properties", "Takes (" + takes.arraySize + ")" });
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider, GUILayout.Height(0.25f));

            EditorGUILayout.Space();

            switch (currentTab)
            {
                case 1:
                    DrawTakesTab();
                    break;
                default:
                    DrawPropertiesTab();
                    break;
            }

            EditorGUILayout.Space();
            me.ApplyModifiedProperties();
        }

        #endregion

        #region<---- SETUP METHODS ---->

        private void SetStyles()
        {
            deleteButtonSkin = 
                (GUISkin)AssetDatabase.LoadAssetAtPath("Assets/_beenoculus/Scripts/Core/Resources/Skins/DeleteButtonSkin.guiskin", typeof(GUISkin));

            GUIStyle foldout;
            try
            {
                foldout = EditorStyles.foldout;
            }
            catch (Exception)
            {
                return;
            }

            var myStyleColor = new Color(255, 163, 26);
            if (foldoutStyle == null)
                foldoutStyle = new GUIStyle(foldout);

            foldoutStyle.fontStyle = FontStyle.Bold;
            foldoutStyle.normal.textColor = myStyleColor;
            foldoutStyle.onNormal.textColor = myStyleColor;
            foldoutStyle.hover.textColor = myStyleColor;
            foldoutStyle.onHover.textColor = myStyleColor;
            foldoutStyle.focused.textColor = myStyleColor;
            foldoutStyle.onFocused.textColor = myStyleColor;
            foldoutStyle.active.textColor = myStyleColor;
            foldoutStyle.onActive.textColor = myStyleColor;

            unselectedGUIColor = new Color(0.85f, 0.85f, 0.85f, 1);
            selectedGUIColor = new Color(255, 255, 255, 1);
            defaultGUIColor = GUI.color;
        }

        #endregion

        #region<---- TAB METHODS ---->

        private void DrawPropertiesTab()
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Total N. of Takes: ", takes.arraySize.ToString());

            if (!EditorApplication.isPlaying)
                EditorGUILayout.LabelField("Current Take: ", "Take System is Not Playing");
            else if (currentTake != null)
                EditorGUILayout.LabelField("Current Take: ", currentTake.FindPropertyRelative("takeName").stringValue);
        }

        private void DrawTakesTab()
        {
            EditorGUILayout.Space();

            if (takes.arraySize <= 0)
            {
                DrawTakesTabWhenEmpty();
                return;
            }

            DrawTakesTabWhenFull();
        }

        private void DrawTakesTabWhenEmpty()
        {
            if (GUILayout.Button("New Take"))
            {
                takes.InsertArrayElementAtIndex(0);
                takes.GetArrayElementAtIndex(0).FindPropertyRelative("autoFinish").boolValue = true;
            }
        }

        private void DrawTakesTabWhenFull()
        {
            for (var i = 0; i < takes.arraySize; i++)
            {
                var beeTakeSP = takes.GetArrayElementAtIndex(i);
                if (beeTakeSP == null) continue;

                var beeTake = beeTakeSP.BGetValue() as Bvu_Take;
                if (beeTake == null) continue;

                if (beeTakeIndexToShow < 0)
                    GUI.color = unselectedGUIColor;
                else
                    GUI.color = beeTakeIndexToShow != i ? unselectedGUIColor : selectedGUIColor;

                GUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    GUILayout.BeginHorizontal();
                    {
                        EditorGUI.indentLevel++;

                        showBeeTake = beeTakeIndexToShow == i;
                        showBeeTake =
                            EditorGUILayout.Foldout(showBeeTake, beeTake.takeName == null || beeTake.takeName.Length <= 0 ? "New Take " + i : beeTake.takeName);

                        if (showBeeTake && beeTakeIndexToShow != i)
                            beeTakeIndexToShow = i;
                        else if (!showBeeTake && beeTakeIndexToShow == i)
                            beeTakeIndexToShow = -1;

                        EditorGUILayout.Space();

                        if (!TryDrawTakeHeader(i))
                        {
                            EditorGUI.indentLevel--;
                            GUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                            return;
                        }

                        EditorGUI.indentLevel--;
                    }
                    GUILayout.EndHorizontal();

                    if (showBeeTake && beeTakeIndexToShow == i)
                        DrawTakeItem(beeTakeSP);
                }
                GUILayout.EndVertical();
            }

            GUI.color = defaultGUIColor;

            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.Space();

                if (GUILayout.Button("New Take"))
                {
                    var index = takes.arraySize;
                    takes.InsertArrayElementAtIndex(index);
                    takes.GetArrayElementAtIndex(index).FindPropertyRelative("autoFinish").boolValue = true;
                }

                EditorGUILayout.Space();
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private bool TryDrawTakeHeader(int i)
        {
            if (showBeeTake && beeTakeIndexToShow == i)
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), deleteButtonSkin.button, GUILayout.MaxHeight(16), GUILayout.MaxWidth(16)))
                {
                    takes.DeleteArrayElementAtIndex(i);
                    return false;
                }

                return true;
            }

            if (i != 0 && GUILayout.Button(EditorGUIUtility.IconContent("CollabPush"), deleteButtonSkin.button, GUILayout.MaxHeight(16), GUILayout.MaxWidth(16)))
            {
                ChangeOrder(i, -1);
            }
            else if (i != takes.arraySize - 1 && GUILayout.Button(EditorGUIUtility.IconContent("CollabPull"), deleteButtonSkin.button, GUILayout.MaxHeight(16), GUILayout.MaxWidth(16)))
            {
                ChangeOrder(i, 1);
            }

            return true;
        }

        private static void DrawTakeItem(SerializedProperty beeTakeSP)
        {
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("takeName"));
            EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("autoFinish"));

            if (!beeTakeSP.FindPropertyRelative("autoFinish").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("conditions"), true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("startDelayInSecs"));
            EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("finishDelayInSecs"));

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("onTakeStart"));
            EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("onTakeUpdate"));
            EditorGUILayout.PropertyField(beeTakeSP.FindPropertyRelative("onTakeFinish"));
        }

        #endregion

        #region<---- LOGICS ---->

        public void ChangeOrder(int currentIndex, int direction)
        {
            beeTakeIndexToShow = -1;
            showBeeTake = false;

            var newIndex = currentIndex + (direction >= 0 ? 1 : -1);
            takes.MoveArrayElement(currentIndex, newIndex);
        }

        #endregion
    }

#endif

    #endregion
}