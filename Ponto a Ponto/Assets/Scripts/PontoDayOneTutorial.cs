using UnityEngine;

[DefaultExecutionOrder(-60)]
public class PontoDayOneTutorial : MonoBehaviour
{
    private enum Step
    {
        Welcome,
        Stops,
        Route,
        Fleet,
        StartDay,
        FirstTrip,
        Finish
    }

    private enum Action
    {
        None,
        Next,
        Skip,
        ToggleMinimized
    }

    public static PontoDayOneTutorial Instance { get; private set; }

    [SerializeField]
    private PontoGame game;
    [SerializeField]
    private bool showOnFirstDay = true;
    private Step step;
    private Action pendingAction;
    private bool finished, minimized;
    private int blockedFrame = -1;
    private int lineRevision = -1, fleetRevision = -1;
    private int coveredRegions, requiredRegions;
    private bool usefulLine, usefulBus;
    private GUIStyle title, body, status, button;
    private static readonly Rect Panel = new Rect(390f, 530f, 820f, 245f);
    private static readonly Rect MinimizedPanel = new Rect(390f, 731f, 820f, 44f);
    private float Scale => Mathf.Max(0.01f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
    private Vector2 Origin => new Vector2((Screen.width - 1600f * Scale) * 0.5f, (Screen.height - 900f * Scale) * 0.5f);
    public bool IsActive => isActiveAndEnabled && showOnFirstDay && !finished && game != null && game.Day == 1 && game.State != PontoGame.Phase.GameOver;
    private bool IsVisible => IsActive && !game.ShowSummary && !(PontoOptionsMenu.Instance != null && PontoOptionsMenu.Instance.IsOpen) && !(PontoHUD.Instance != null && !PontoHUD.Instance.IsVisible);
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

    public void RestartTutorial()
    {
        step = Step.Welcome;
        pendingAction = Action.None;
        finished = minimized = false;
        lineRevision = fleetRevision = -1;
        coveredRegions = requiredRegions = 0;
        usefulLine = usefulBus = false;
    }

    private void Update()
    {
        if (!IsActive)
            return;
        RefreshGoals();
        switch (pendingAction)
        {
            case Action.Next:
                if (Ready)
                {
                    if (step == Step.Finish)
                        finished = true;
                    else
                        step = (Step)((int)step + 1);
                }

                break;
            case Action.Skip:
                finished = true;
                break;
            case Action.ToggleMinimized:
                minimized = !minimized;
                break;
        }

        if (pendingAction != Action.None)
            blockedFrame = Time.frameCount;
        pendingAction = Action.None;
        if (game.HasFinishedDay || game.Completed > 0)
            step = Step.Finish;
        else if ((game.State == PontoGame.Phase.Running || game.State == PontoGame.Phase.Closing) && (int)step < (int)Step.FirstTrip)
            step = Step.FirstTrip;
    }

    private void RefreshGoals()
    {
        if (game.selector == null || game.lineBuilder == null || game.fleet == null)
            return;
        if (lineRevision == game.lineBuilder.Revision && fleetRevision == game.fleet.Revision)
            return;
        lineRevision = game.lineBuilder.Revision;
        fleetRevision = game.fleet.Revision;
        coveredRegions = requiredRegions = 0;
        usefulLine = usefulBus = false;
        if (game.regions != null)
            foreach (var region in game.regions)
            {
                if (region == null || !region.IsActive(1))
                    continue;
                requiredRegions++;
                foreach (var stop in game.selector.Stops)
                    if (region.Covers(stop.transform.position))
                    {
                        coveredRegions++;
                        break;
                    }
            }

        foreach (var line in game.lineBuilder.Lines)
        {
            if (!CoversStartingRegions(line))
                continue;
            usefulLine = true;
            foreach (var bus in game.fleet.Buses)
                if (bus != null && bus.Line == line)
                {
                    usefulBus = true;
                    break;
                }
        }
    }

    private bool CoversStartingRegions(RoadLineBuilder.LineData line)
    {
        if (line.stops == null || line.stops.Count < 2 || game.regions == null || requiredRegions < 2)
            return false;
        foreach (var region in game.regions)
        {
            if (region == null || !region.IsActive(1))
                continue;
            bool found = false;
            foreach (Vector3Int cell in line.stops)
            {
                BusStop stop = game.selector.StopAt(cell);
                if (stop != null && region.Covers(stop.transform.position))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    private bool Ready
    {
        get
        {
            switch (step)
            {
                case Step.Stops:
                    return game.selector != null && game.selector.Stops.Count >= 2 && requiredRegions >= 2 && coveredRegions >= requiredRegions;
                case Step.Route:
                    return usefulLine;
                case Step.Fleet:
                    return usefulBus;
                case Step.StartDay:
                    return game.State == PontoGame.Phase.Running || game.State == PontoGame.Phase.Closing;
                case Step.FirstTrip:
                    return game.Completed > 0 || game.HasFinishedDay;
                default:
                    return true;
            }
        }
    }

    private string Title
    {
        get
        {
            switch (step)
            {
                case Step.Stops:
                    return "Coloque suas primeiras paradas";
                case Step.Route:
                    return "Desenhe uma linha";
                case Step.Fleet:
                    return "Confira o ônibus da linha";
                case Step.StartDay:
                    return "Inicie o primeiro dia";
                case Step.FirstTrip:
                    return "Acompanhe a primeira viagem";
                case Step.Finish:
                    return "Meta, receita e melhorias";
                default:
                    return "Bem-vindo ao Ponto a Ponto";
            }
        }
    }

    private string Description
    {
        get
        {
            switch (step)
            {
                case Step.Stops:
                    return "Selecione 1 — Paradas e coloque pontos nas ruas, dentro de cada região ativa. " + "As áreas coloridas mostram o alcance a pé dos bairros. Atenda todas as regiões " + "iniciais para conseguir transportar passageiros entre seus destinos.";
                case Step.Route:
                    return "Selecione 2 — Linhas. Segure o botão esquerdo e desenhe pelas ruas, passando " + "pelos pontos de todas as regiões iniciais. Volte à célula inicial e confirme com " + "Enter. Arrastar de volta pelo último trecho apaga o desenho.";
                case Step.Fleet:
                    return "A primeira linha recebe um ônibus da garagem automaticamente. Confira a aba " + "Frota. Se a linha estiver sem ônibus, use Adicionar ônibus da garagem na aba " + "Linhas. O ônibus atende todas as paradas do percurso.";
                case Step.StartDay:
                    return "Clique em INICIAR DIA 1 no rodapé. Os bairros vão gerar passageiros com " + "destinos em outras regiões. A meta depende dessa demanda: criar muitos pontos " + "no mesmo bairro não gera mais pessoas.";
                case Step.FirstTrip:
                    return "Observe os passageiros nas paradas e a lotação na aba Frota. A receita só é " + "registrada no destino final. Linhas com uma parada compartilhada permitem " + "baldeação. Espaço pausa; o botão Velocidade acelera o dia.";
                case Step.Finish:
                    return "Alcance a meta de receita. O dinheiro entra no saldo ao encerrar o dia; no " + "planejamento, compre ônibus e melhore capacidade, velocidade e embarque. " + "Você perde após " + game.maxFailedDays + " dias abaixo da meta, mesmo que não sejam consecutivos.";
                default:
                    return "Sua missão é conectar os bairros com transporte público. Vamos montar uma " + "primeira linha e transportar alguém. Você pode minimizar este guia para ver o " + "mapa ou pular o tutorial e jogar livremente.";
            }
        }
    }

    private string Status
    {
        get
        {
            switch (step)
            {
                case Step.Stops:
                    return "Regiões ativas com parada: " + coveredRegions + " / " + requiredRegions;
                case Step.Route:
                    return usefulLine ? "Linha conectando os bairros confirmada!" : "Confirme um circuito passando pelas regiões iniciais.";
                case Step.Fleet:
                    return usefulBus ? "Ônibus atribuído à linha!" : "Atribua um ônibus à linha que conecta os bairros.";
                case Step.StartDay:
                    return "Inicie o dia pelo botão no rodapé.";
                case Step.FirstTrip:
                    return "Viagens concluídas: " + game.Completed + " | Receita: R$ " + game.Revenue + " / " + game.Target;
                case Step.Finish:
                    return "Acompanhe filas e bairros sem atendimento para decidir onde investir.";
                default:
                    return "WASD: câmera | Botão do meio: arrastar | Rodinha: zoom";
            }
        }
    }

    public bool ContainsMouse()
    {
        if (blockedFrame == Time.frameCount)
            return true;
        if (!IsVisible)
            return false;
        Vector2 mouse = PontoInput.MousePosition;
        Vector2 point = (new Vector2(mouse.x, Screen.height - mouse.y) - Origin) / Scale;
        return (minimized ? MinimizedPanel : Panel).Contains(point);
    }

    private void Styles()
    {
        if (title != null)
            return;
        title = new GUIStyle(GUI.skin.label)
        {fontSize = 22, fontStyle = FontStyle.Bold};
        body = new GUIStyle(GUI.skin.label)
        {fontSize = 18, wordWrap = true};
        status = new GUIStyle(body)
        {fontSize = 16};
        button = new GUIStyle(GUI.skin.button)
        {fontSize = 17, wordWrap = true};
    }

    private bool Button(Rect rect, string text)
    {
        bool clicked = GUI.Button(rect, text, button);
        if (clicked)
            PontoAudio.Instance?.PlayClick();
        return clicked;
    }

    private void OnGUI()
    {
        if (!IsVisible)
            return;
        Styles();
        Matrix4x4 previousMatrix = GUI.matrix;
        int previousDepth = GUI.depth;
        bool previousEnabled = GUI.enabled;
        GUI.matrix = Matrix4x4.TRS(Origin, Quaternion.identity, Vector3.one * Scale);
        GUI.depth = -50;
        try
        {
            if (minimized)
            {
                if (Button(MinimizedPanel, "Tutorial " + ((int)step + 1) + "/7 — " + Title + " (expandir)"))
                    pendingAction = Action.ToggleMinimized;
                return;
            }

            GUI.Box(Panel, "");
            GUI.Label(new Rect(408f, 544f, 784f, 32f), ((int)step + 1) + "/7 — " + Title, title);
            GUI.Label(new Rect(408f, 580f, 784f, 96f), Description, body);
            GUI.Label(new Rect(408f, 682f, 784f, 34f), Status, status);
            GUI.enabled = previousEnabled && Ready;
            if (Button(new Rect(408f, 729f, 350f, 32f), step == Step.Finish ? "Concluir tutorial" : "Continuar"))
                pendingAction = Action.Next;
            GUI.enabled = previousEnabled;
            if (Button(new Rect(772f, 729f, 190f, 32f), "Minimizar"))
                pendingAction = Action.ToggleMinimized;
            if (Button(new Rect(976f, 729f, 216f, 32f), "Pular tutorial"))
                pendingAction = Action.Skip;
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
            GUI.enabled = previousEnabled;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
