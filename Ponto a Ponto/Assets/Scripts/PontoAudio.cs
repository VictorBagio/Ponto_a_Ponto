using UnityEngine;

public class PontoAudio : MonoBehaviour
{
    public static PontoAudio Instance { get; private set; }

    [Header("Audios")]
    [SerializeField]
    private AudioClip backgroundMusic;
    [SerializeField]
    private AudioClip clickSound;
    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)]
    private float musicVolume = 0.35f;
    [SerializeField, Range(0f, 1f)]
    private float clickVolume = 0.7f;
    private AudioSource musicSource;
    private AudioSource effectsSource;
    private const string MusicKey = "PontoAPonto.MusicVolume";
    private const string ClickKey = "PontoAPonto.ClickVolume";
    public float MusicVolume
    {
        get => musicVolume;
        set
        {
            musicVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }
    }

    public float ClickVolume
    {
        get => clickVolume;
        set
        {
            clickVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, musicVolume));
        clickVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(ClickKey, clickVolume));
        musicSource = CreateSource(true);
        effectsSource = CreateSource(false);
        ApplyVolumes();
    }

    private void Start()
    {
        PlayMusic();
    }

    private AudioSource CreateSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
        source.pitch = 1f;
        source.dopplerLevel = 0f;
        return source;
    }

    public void PlayMusic()
    {
        if (musicSource == null || backgroundMusic == null)
            return;
        if (musicSource.isPlaying && musicSource.clip == backgroundMusic)
            return;
        musicSource.clip = backgroundMusic;
        musicSource.Play();
    }

    public void PlayClick()
    {
        if (effectsSource != null && clickSound != null)
            effectsSource.PlayOneShot(clickSound);
    }

    private void ApplyVolumes()
    {
        if (musicSource != null)
            musicSource.volume = musicVolume;
        if (effectsSource != null)
            effectsSource.volume = clickVolume;
    }

    private void OnValidate()
    {
        musicVolume = Mathf.Clamp01(musicVolume);
        clickVolume = Mathf.Clamp01(clickVolume);
        ApplyVolumes();
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(MusicKey, musicVolume);
        PlayerPrefs.SetFloat(ClickKey, clickVolume);
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        if (Instance == this)
            SaveSettings();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        if (musicSource != null)
            Destroy(musicSource);
        if (effectsSource != null)
            Destroy(effectsSource);
        Instance = null;
    }
}
