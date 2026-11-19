using UnityEngine;

[DefaultExecutionOrder(-150)]
public class PontoOptionsMenu : MonoBehaviour
{
    private enum Page
    {
        Main,
        Settings,
        Controls
    }

    public static PontoOptionsMenu Instance { get; private set; }

    [SerializeField]
    private PontoGame game;
    private Page page;
    private bool wasPaused;
    private int blockedFrame = -1;
    private Vector2 controlsScroll;
    private GUIStyle title, label, button;
    private string quitMessage = "";
    private System.Action pendingAction;
    public bool IsOpen { get; private set; }

    public bool BlocksGameplayInput => IsOpen || blockedFrame == Time.frameCount;
    private float Scale => Mathf.Max(0.01f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
    private float Width => Screen.width / Scale;
    private float Height => Screen.height / Scale;
    private Rect MenuButton => new Rect(Width - 142f, 12f, 130f, 38f);
    private Rect HudButton => new Rect(Width - 314f, 12f, 160f, 38f);
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (game == null)
            game = Object.FindFirstObjectByType<PontoGame>();
    }

    private void Update()
    {
        if (pendingAction != null)
        {
            var action = pendingAction;
            pendingAction = null;
            blockedFrame = Time.frameCount;
            action();
            return;
        }

        if (!PontoInput.RawPressed(KeyCode.Escape))
            return;
        blockedFrame = Time.frameCount;
        if (IsOpen)
        {
            if (page == Page.Main)
                CloseMenu();
            else
                page = Page.Main;
        }
        else if (game != null && game.CanBuild && game.lineBuilder != null && game.lineBuilder.HasDraft)
            game.lineBuilder.CancelLine();
        else
            OpenMenu();
    }

    public bool ContainsMouse()
    {
        if (BlocksGameplayInput)
            return true;
        Vector2 mouse = PontoInput.MousePosition;
        Vector2 point = new Vector2(mouse.x, Screen.height - mouse.y) / Scale;
        return MenuButton.Contains(point) || HudButton.Contains(point);
    }

    public void OpenMenu()
    {
        if (IsOpen)
            return;
        wasPaused = game != null && game.Paused;
        if (CanPauseGame() && !game.Paused)
            game.TogglePause();
        page = Page.Main;
        quitMessage = "";
        blockedFrame = Time.frameCount;
        IsOpen = true;
    }

    public void CloseMenu()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        blockedFrame = Time.frameCount;
        PontoAudio.Instance?.SaveSettings();
        if (CanPauseGame() && game.Paused != wasPaused)
            game.TogglePause();
    }

    private bool CanPauseGame() => game != null && (game.State == PontoGame.Phase.Running || game.State == PontoGame.Phase.Closing);
    private void Styles()
    {
        if (title != null)
            return;
        title = new GUIStyle(GUI.skin.label)
        {fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter};
        label = new GUIStyle(GUI.skin.label)
        {fontSize = 19, wordWrap = true};
        button = new GUIStyle(GUI.skin.button)
        {fontSize = 19, wordWrap = true};
    }

    private bool Button(string text, float height = 48f)
    {
        bool clicked = GUILayout.Button(text, button, GUILayout.Height(height));
        if (clicked)
            PontoAudio.Instance?.PlayClick();
        return clicked;
    }

    private bool FixedButton(Rect rect, string text)
    {
        bool clicked = GUI.Button(rect, text, button);
        if (clicked)
            PontoAudio.Instance?.PlayClick();
        return clicked;
    }

    private void OnGUI()
    {
        Styles();
        Matrix4x4 previousMatrix = GUI.matrix;
        int previousDepth = GUI.depth;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);
        GUI.depth = -100;
        try
        {
            if (IsOpen)
                DrawMenu();
            if (FixedButton(MenuButton, IsOpen ? "Fechar menu" : "Menu"))
            {
                if (IsOpen)
                    pendingAction = CloseMenu;
                else
                    pendingAction = OpenMenu;
            }

            var hud = PontoHUD.Instance;
            if (FixedButton(HudButton, hud != null && !hud.IsVisible ? "Mostrar HUD" : "Ocultar HUD"))
            {
                pendingAction = () => hud?.ToggleVisibility();
            }
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
        }
    }

    private void DrawMenu()
    {
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Width, Height), Texture2D.whiteTexture);
        GUI.color = previousColor;
        Rect panel = new Rect((Width - 560f) * 0.5f, (Height - 520f) * 0.5f, 560f, 520f);
        GUI.Box(panel, "");
        GUILayout.BeginArea(new Rect(panel.x + 28f, panel.y + 22f, panel.width - 56f, panel.height - 44f));
        GUILayout.Label(page == Page.Main ? "MENU" : page == Page.Settings ? "CONFIGURAÇÕES" : "CONTROLES", title);
        GUILayout.Space(22f);
        switch (page)
        {
            case Page.Main:
                if (Button("Continuar"))
                    pendingAction = CloseMenu;
                if (Button("Configurações"))
                    pendingAction = () => page = Page.Settings;
                if (Button("Controles"))
                    pendingAction = () => page = Page.Controls;
                if (Button("Fechar jogo"))
                    pendingAction = QuitGame;
                if (quitMessage.Length > 0)
                    GUILayout.Label(quitMessage, label);
                break;
            case Page.Settings:
                DrawSettings();
                GUILayout.FlexibleSpace();
                if (Button("Voltar"))
                    pendingAction = () => page = Page.Main;
                break;
            case Page.Controls:
                controlsScroll = GUILayout.BeginScrollView(controlsScroll);
                GUILayout.Label("1: colocar paradas\n" + "2: desenhar linhas\n" + "3: remover paradas ou linhas\n\n" + "Botão esquerdo segurado: desenhar pelas ruas\n" + "Arrastar de volta: desfazer o último trecho\n" + "Enter: confirmar circuito fechado\n" + "Backspace: apagar a última célula\n" + "Esc: cancelar desenho ou abrir/fechar menu\n\n" + "WASD: mover câmera\n" + "Botão do meio segurado: arrastar câmera\n" + "Rodinha: zoom\n" + "Espaço: pausar ou continuar a operação", label);
                GUILayout.EndScrollView();
                if (Button("Voltar"))
                    pendingAction = () => page = Page.Main;
                break;
        }

        GUILayout.EndArea();
    }

    private void DrawSettings()
    {
        var audio = PontoAudio.Instance;
        if (audio == null)
        {
            GUILayout.Label("Áudio indisponível.", label);
            return;
        }

        GUILayout.Label("Música: " + Mathf.RoundToInt(audio.MusicVolume * 100f) + "%", label);
        float music = GUILayout.HorizontalSlider(audio.MusicVolume, 0f, 1f);
        if (!Mathf.Approximately(music, audio.MusicVolume))
            audio.MusicVolume = music;
        GUILayout.Space(24f);
        GUILayout.Label("Efeitos: " + Mathf.RoundToInt(audio.ClickVolume * 100f) + "%", label);
        float effects = GUILayout.HorizontalSlider(audio.ClickVolume, 0f, 1f);
        if (!Mathf.Approximately(effects, audio.ClickVolume))
            audio.ClickVolume = effects;
    }

    private void QuitGame()
    {
        PontoAudio.Instance?.SaveSettings();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        quitMessage = "Para sair, feche a aba ou a janela do jogo.";
#else
        Application.Quit();
#endif
    }

    private void OnDisable()
    {
        pendingAction = null;
        CloseMenu();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
