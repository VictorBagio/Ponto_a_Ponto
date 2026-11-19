using System.Collections.Generic;
using UnityEngine;

public class PontoHUD : MonoBehaviour
{
    public static PontoHUD Instance { get; private set; }

    public PontoGame game;
    public bool IsVisible { get; private set; } = true;
    public void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    private int tab, selectedBus;
    private Vector2 listScroll;
    private GUIStyle title, normal, small, button;
    private static readonly Rect Header = new Rect(0, 0, 1600, 94);
    private static readonly Rect Sidebar = new Rect(14, 108, 355, 650);
    private static readonly Rect Details = new Rect(1231, 108, 355, 650);
    private static readonly Rect Footer = new Rect(0, 792, 1600, 108);
    private float Scale => Mathf.Max(0.01f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
    private Vector2 Origin => new Vector2((Screen.width - 1600 * Scale) / 2, (Screen.height - 900 * Scale) / 2);
    private void Awake()
    {
        Instance = this;
        if (Object.FindFirstObjectByType<PontoOptionsMenu>() == null)
            gameObject.AddComponent<PontoOptionsMenu>();
        if (Object.FindFirstObjectByType<PontoDayOneTutorial>() == null)
            gameObject.AddComponent<PontoDayOneTutorial>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool ContainsMouse()
    {
        if (game != null && (game.ShowSummary || game.State == PontoGame.Phase.GameOver))
            return true;
        if (!IsVisible)
            return false;
        Vector2 m = PontoInput.MousePosition;
        Vector2 p = (new Vector2(m.x, Screen.height - m.y) - Origin) / Scale;
        return Header.Contains(p) || Sidebar.Contains(p) || Details.Contains(p) || Footer.Contains(p);
    }

    private void Styles()
    {
        if (title != null)
            return;
        title = new GUIStyle(GUI.skin.label)
        {fontSize = 25, fontStyle = FontStyle.Bold, wordWrap = true};
        normal = new GUIStyle(GUI.skin.label)
        {fontSize = 18, wordWrap = true};
        small = new GUIStyle(normal)
        {fontSize = 15};
        button = new GUIStyle(GUI.skin.button)
        {fontSize = 17, wordWrap = true};
    }

    private bool Button(string text, bool enabled = true, float height = 38)
    {
        bool old = GUI.enabled;
        GUI.enabled = old && enabled;
        bool result = GUILayout.Button(text, button, GUILayout.Height(height));
        GUI.enabled = old;
        if (result)
            PontoAudio.Instance?.PlayClick();
        return result;
    }

    private void Label(string value)
    {
        GUILayout.Label(value, normal);
    }

    private void OnGUI()
    {
        if (game == null || game.fleet == null || game.lineBuilder == null || game.selector == null)
            return;
        Styles();
        Matrix4x4 previousMatrix = GUI.matrix;
        bool previousEnabled = GUI.enabled;
        GUI.matrix = Matrix4x4.TRS(Origin, Quaternion.identity, Vector3.one * Scale);
        GUI.enabled = previousEnabled && !(PontoOptionsMenu.Instance != null && PontoOptionsMenu.Instance.IsOpen);
        try
        {
            if (IsVisible)
                DrawHUD();
            else if (game.ShowSummary || game.State == PontoGame.Phase.GameOver)
                Summary();
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.enabled = previousEnabled;
        }
    }

    private void DrawHeader()
    {
        GUILayout.BeginArea(new Rect(20, 8, 1220, 38));
        GUILayout.BeginHorizontal();
        GUILayout.Label("PONTO A PONTO", title, GUILayout.Width(280));
        GUILayout.Label("Dia " + game.Day + " | " + PhaseLabel(), normal, GUILayout.Width(260));
        GUILayout.Label("Saldo: R$ " + game.Cash + "   Receita: R$ " + game.Revenue + " / " + game.Target, normal);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
        int seconds = Mathf.CeilToInt(game.Remaining);
        GUILayout.BeginArea(new Rect(20, 52, 1560, 32));
        GUILayout.Label("Tempo: " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00") + "     Viagens: " + game.Completed + "     Esperando: " + game.WaitingCount + "     A bordo: " + game.fleet.OnboardCount + "     Dias abaixo da meta: " + game.FailedDays + "/" + game.maxFailedDays, normal);
        GUILayout.EndArea();
    }

    private void DrawHUD()
    {
        if (game == null || game.fleet == null || game.lineBuilder == null || game.selector == null)
            return;
        GUI.Box(Header, "");
        GUI.Box(Sidebar, "");
        GUI.Box(Details, "");
        GUI.Box(Footer, "");
        DrawHeader();
        GUILayout.BeginArea(new Rect(26, 120, 331, 626));
        int previousTab = tab;
        tab = GUILayout.Toolbar(tab, new[]{"Linhas", "Frota", "Paradas", "Regioes"}, GUILayout.Height(36));
        if (tab != previousTab)
            PontoAudio.Instance?.PlayClick();
        listScroll = GUILayout.BeginScrollView(listScroll);
        if (tab == 0)
        {
            foreach (var line in game.lineBuilder.Lines)
            {
                Color old = GUI.contentColor;
                GUI.contentColor = line.color;
                GUILayout.Label(line.Label, title);
                GUI.contentColor = old;
                int count = 0;
                foreach (var bus in game.fleet.Buses)
                    if (bus.Line == line)
                        count++;
                Label(line.stops.Count + " paradas | " + count + " ônibus");
                if (Button("Adicionar ônibus da garagem", game.CanBuild))
                    game.fleet.AssignAvailableTo(line);
                GUILayout.Space(12);
            }

            if (game.lineBuilder.Lines.Count == 0)
                Label("Modo 2: segure o mouse e desenhe pelas ruas. Feche o circuito e confirme com Enter.");
        }
        else if (tab == 1)
        {
            for (int i = 0; i < game.fleet.Buses.Count; i++)
            {
                var bus = game.fleet.Buses[i];
                string route = bus.Line == null ? "Garagem" : bus.Line.Label;
                if (Button("Ônibus " + bus.Number + " — " + route + "\n" + bus.Passengers.Count + "/" + bus.Capacity + " passageiros", true, 60))
                    selectedBus = i;
            }

            if (Button("Comprar ônibus — R$ " + game.fleet.newBusCost, game.CanBuild))
                game.fleet.BuyBus();
        }
        else if (tab == 2)
        {
            foreach (var stop in game.selector.Stops)
            {
                Color old = GUI.contentColor;
                GUI.contentColor = stop.DestinationColor;
                GUILayout.Label(stop.Label + " — " + stop.Waiting.Count + " esperando", title);
                GUI.contentColor = old;
                var destinations = new Dictionary<string, int>();
                foreach (var p in stop.Waiting)
                {
                    if (p.destination == null)
                        continue;
                    string key = p.destination.Label;
                    destinations[key] = destinations.TryGetValue(key, out int count) ? count + 1 : 1;
                }

                foreach (var pair in destinations)
                    GUILayout.Label("Destino " + pair.Key + ": " + pair.Value, small);
                GUILayout.Space(10);
            }
        }
        else
        {
            Label("Demanda diária: " + game.DemandForDay(game.DisplayDay) + " viagens");
            Label("Sem espaço ou sem ponto: " + game.OutsideStopCount);
            if (game.regions != null)
                foreach (var region in game.regions)
                {
                    if (region == null)
                        continue;
                    Color old = GUI.contentColor;
                    GUI.contentColor = region.regionColor;
                    GUILayout.Label(region.Label + " " + region.regionName, title);
                    GUI.contentColor = old;
                    if (!region.IsActive(game.DisplayDay))
                    {
                        Label("Disponível no dia " + region.activationDay);
                        GUILayout.Space(10);
                        continue;
                    }

                    Label(game.RequestsForDay(region, game.DisplayDay) + " viagens por dia");
                    Label(game.FindStopFor(region) == null ? "SEM PONTO DISPONÍVEL" : "Com ponto próximo");
                    Label("No bairro: " + region.Pending.Count);
                    Label("Geradas hoje: " + region.Demand.Issued + "/" + region.Demand.Quota);
                    GUILayout.Space(12);
                }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(1245, 120, 327, 626));
        GUILayout.Label("ÔNIBUS SELECIONADO", title);
        if (game.fleet.Buses.Count > 0)
        {
            selectedBus = Mathf.Clamp(selectedBus, 0, game.fleet.Buses.Count - 1);
            BusMovement bus = game.fleet.Buses[selectedBus];
            Label("Ônibus " + bus.Number);
            Label(bus.Line == null ? "Disponível na garagem" : bus.Line.Label);
            Label("Lotação: " + bus.Passengers.Count + "/" + bus.Capacity);
            Label("Velocidade: " + bus.Speed.ToString("0.0"));
            Label("Parada: " + bus.StopDuration.ToString("0.0") + " s");
            GUILayout.Space(14);
            UpgradeButton(bus, 0, "Capacidade", bus.CapacityLevel);
            UpgradeButton(bus, 1, "Velocidade", bus.SpeedLevel);
            UpgradeButton(bus, 2, "Embarque", bus.BoardingLevel);
            GUILayout.Space(14);
            if (Button("Devolver à garagem", game.CanBuild && bus.Line != null))
                game.fleet.ReturnBus(bus);
            GUILayout.Space(12);
            GUILayout.Label("Destino dos passageiros a bordo:", small);
            string text = "";
            int n = 0;
            foreach (var p in bus.Passengers)
            {
                if (p.destination != null)
                    text += p.destination.Label + " ";
                if (++n >= 20)
                    break;
            }

            GUILayout.Label(text.Length == 0 ? "Nenhum passageiro." : text, small);
        }

        GUILayout.EndArea();
        GUILayout.BeginArea(new Rect(18, 800, 1564, 92));
        GUILayout.BeginHorizontal();
        if (Button("1  Paradas", game.CanBuild))
            game.selector.SetMode(0);
        if (Button("2  Linhas", game.CanBuild))
            game.selector.SetMode(1);
        if (Button("3  Remover", game.CanBuild))
            game.selector.SetMode(2);
        if (Button("Confirmar linha", game.CanBuild && game.selector.Mode == RoadGridSelector.ToolMode.Lines))
            game.lineBuilder.ConfirmLine();
        if (Button(game.Paused ? "Continuar" : "Pausar", game.State == PontoGame.Phase.Running || game.State == PontoGame.Phase.Closing))
            game.TogglePause();
        if (Button("Velocidade " + game.SimulationSpeed + "x"))
            game.CycleSpeed();
        int nextDay = game.HasFinishedDay ? game.Day + 1 : game.Day;
        if (Button("INICIAR DIA " + nextDay, game.CanBuild))
            game.BeginDay();
        GUILayout.EndHorizontal();
        string modeText = game.selector.Mode == RoadGridSelector.ToolMode.Stops ? "Paradas" : game.selector.Mode == RoadGridSelector.ToolMode.Lines ? "Linhas" : "Remover";
        GUILayout.Label("Modo: " + modeText + "  |  " + game.Message, small);
        if (game.CanBuild)
            GUILayout.Label("Próxima meta: R$ " + game.TargetForDay(nextDay) + " | Demanda: " + game.DemandForDay(nextDay) + " viagens | WASD: câmera | botão do meio: arrastar | rodinha: zoom", small);
        GUILayout.EndArea();
        if (game.ShowSummary || game.State == PontoGame.Phase.GameOver)
            Summary();
    }

    private void UpgradeButton(BusMovement bus, int type, string label, int level)
    {
        int cost = bus.UpgradeCost(type);
        string text = label + " — nível " + (level + 1) + (cost < 0 ? " (máximo)" : "\nMelhorar: R$ " + cost);
        if (Button(text, game.CanBuild && cost >= 0, 56))
            bus.Upgrade(type);
    }

    private string PhaseLabel()
    {
        switch (game.State)
        {
            case PontoGame.Phase.Planning:
                return "Planejamento";
            case PontoGame.Phase.Running:
                return game.Paused ? "Pausado" : "Operação";
            case PontoGame.Phase.Closing:
                return "Finalizando viagens";
            default:
                return "Operação encerrada";
        }
    }

    private void Summary()
    {
        GUI.Box(new Rect(0, 0, 1600, 900), "");
        GUILayout.BeginArea(new Rect(470, 180, 660, 540), GUI.skin.box);
        GUILayout.Space(18);
        GUILayout.Label(game.State == PontoGame.Phase.GameOver ? "OPERAÇÃO ENCERRADA" : "DIA " + game.Day + " CONCLUÍDO", title);
        Label("Viagens concluídas: " + game.Completed);
        Label("Receita: R$ " + game.Revenue + "  |  Meta: R$ " + game.Target);
        Label(game.Revenue >= game.Target ? "Meta atingida." : "Receita abaixo da meta: uma falha acumulada.");
        Label("Falhas acumuladas: " + game.FailedDays + " / " + game.maxFailedDays);
        Label("Saldo disponível: R$ " + game.Cash);
        Label("Passageiros aguardando o próximo dia: " + game.UnservedAtClose);
        Label("Viagens a pé (sem tarifa): " + game.WalkedToday);
        if (game.Completed > 0)
            Label("Tempo médio até o destino: " + (game.TotalTripTime / game.Completed).ToString("0") + " s");
        GUILayout.Space(15);
        if (game.State == PontoGame.Phase.Planning)
        {
            Label("Use a receita para melhorar a frota. As falhas anteriores permanecem.");
            if (Button("PLANEJAR PRÓXIMO DIA", true, 50))
                game.DismissSummary();
        }
        else if (Button("NOVA PARTIDA", true, 50))
            game.ResetGame();
        GUILayout.EndArea();
    }
}
