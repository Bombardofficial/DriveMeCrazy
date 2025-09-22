// DebugSkillOverlay.cs (fixed)
using System.Linq;
using UnityEngine;

public class DebugSkillOverlay : MonoBehaviour
{
    [Header("Show / Hide")]
    public bool visible = true;

    [Header("Layout")]
    public Vector2 margin = new Vector2(14, 14);
    [Range(0.75f, 2f)] public float uiScale = 1.0f;

    [Header("Numbers")]
    public int percentDigits = 0;

    // column widths (scaled)
    float colPlayer = 110f, colSkill = 64f, colSucc = 54f, colMiss = 54f, colHit = 54f;
    const float RowH = 18f;

    GUIStyle _head, _line, _small, _badge, _smallRight, _numRight;
    bool _stylesReady;


    void EnsureStyles()
    {
        if (_stylesReady) return;

        int fs14 = Mathf.RoundToInt(14 * uiScale);
        int fs12 = Mathf.RoundToInt(12 * uiScale);
        int fs11 = Mathf.RoundToInt(11 * uiScale);

        _head = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = fs14 };
        _line = new GUIStyle(GUI.skin.label) { fontSize = fs12 };
        _small = new GUIStyle(GUI.skin.label) { fontSize = fs11, normal = { textColor = new Color(1, 1, 1, 0.8f) } };
        _badge = new GUIStyle(_line) { normal = { textColor = new Color(1f, .85f, .2f) } };
        _smallRight = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight };
        _numRight = new GUIStyle(_line) { alignment = TextAnchor.MiddleRight };

        _stylesReady = true;
    }

    float W(float w) => Mathf.Round(w * uiScale);
    float H(float h) => Mathf.Round(h * uiScale);

    void OnGUI()
    {
        if (!visible) return;
        EnsureStyles();

        var est = SkillEstimator.Instance;
        if (!est) return;
        var dir = DifficultyDirector.Instance;

        // dynamic height
        int rows = Mathf.Max(1, est.Profiles?.Count ?? 0);
        float headBlock = H(90f);
        float tableHead = H(RowH + 6f);
        float tableBody = H(RowH * rows);
        float footer = H(22f);
        float boxH = headBlock + tableHead + tableBody + footer + H(8f);

        float boxW = W(6f + colPlayer + colSkill + colSucc + colMiss + colHit + 6f);

        float x = margin.x;
        float y = Screen.height - margin.y - boxH;

        var rect = new Rect(x, y, boxW, boxH);
        GUI.Box(rect, GUIContent.none);

        GUILayout.BeginArea(rect);
        {
            var activeP = est.ActivePassenger;
            var active = est.Active ?? est.Global;

            GUILayout.Space(H(6f));
            GUILayout.Label("DRIVE-ME-CRAZY: Skill Debug", _head);

            string who = activeP ? $"Player {activeP.PlayerNumber}" : "(no driver)";
            GUILayout.Label($"Current Driver: {who}", _line);

            float skill = active?.Skill01 ?? 0.5f;
            float dOverall = dir ? dir.Current.overall : 0.5f;
            float dPrecision = dir ? dir.Current.precision : 0.5f;
            float dSpawn = dir ? dir.Current.spawnPressure : 0.5f;
            GUILayout.Label($"Skill: {Pct(skill)}  |  Diff overall: {Pct(dOverall)}  |  precision: {Pct(dPrecision)}  |  spawn: {Pct(dSpawn)}", _line);


            GUILayout.Label($"Collect Success: {Pct(active?.CollectSuccessRate ?? 0)}   " +
                            $"Miss: {Pct(active?.CollectMissRate ?? 0)}   " +
                            $"Hit: {Pct(active?.ObstacleHitRate ?? 0)}", _line);

            GUILayout.Space(H(6f));
            GUILayout.Label("Per-Player Profiles", _head);

            // header row
            GUILayout.BeginHorizontal();
            GUILayout.Label("Player", _small, GUILayout.Width(W(colPlayer)));
            GUILayout.Label("Skill", _smallRight, GUILayout.Width(W(colSkill)));
            GUILayout.Label("Succ", _smallRight, GUILayout.Width(W(colSucc)));
            GUILayout.Label("Miss", _smallRight, GUILayout.Width(W(colMiss)));
            GUILayout.Label("Hit", _smallRight, GUILayout.Width(W(colHit)));
            GUILayout.EndHorizontal();

            // rows
            var dict = est.Profiles;
            if (dict != null && dict.Count > 0)
            {
                foreach (var kv in dict.OrderBy(k => k.Key ? k.Key.PlayerNumber : 9999))
                {
                    var p = kv.Key;
                    var pr = kv.Value;
                    if (pr == null) continue;

                    bool isActive = (p && est.ActivePassenger == p);

                    GUILayout.BeginHorizontal(GUILayout.Height(H(RowH)));

                    // Player cell with star for active
                    GUILayout.BeginHorizontal(GUILayout.Width(W(colPlayer)));
                    if (isActive) GUILayout.Label("?", _badge, GUILayout.Width(W(16f)));
                    GUILayout.Label(p ? $"P{p.PlayerNumber}" : "(null)", _line);
                    GUILayout.EndHorizontal();

                    GUILayout.Label(Pct(pr.Skill01), _numRight, GUILayout.Width(W(colSkill)));
                    GUILayout.Label(Pct(pr.CollectSuccessRate), _numRight, GUILayout.Width(W(colSucc)));
                    GUILayout.Label(Pct(pr.CollectMissRate), _numRight, GUILayout.Width(W(colMiss)));
                    GUILayout.Label(Pct(pr.ObstacleHitRate), _numRight, GUILayout.Width(W(colHit)));

                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("(no profiles yet)", _small);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"[F3] toggle | EMA windows: C={est.windowCollect:F1}s  O={est.windowObstacle:F1}s", _small);
        }
        GUILayout.EndArea();
    }

    string Pct(float r) => (r * 100f).ToString($"F{percentDigits}") + "%";
}
