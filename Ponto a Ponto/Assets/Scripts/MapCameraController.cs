using UnityEngine;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(-100)]
public class MapCameraController : MonoBehaviour
{
    [SerializeField, Min(0)]
    private float moveSpeed = 12f;
    [SerializeField, Min(0.1f)]
    private float minZoom = 3f;
    [SerializeField, Min(0.1f)]
    private float maxZoom = 35f;
    [SerializeField, Range(0.01f, 0.5f)]
    private float zoomStep = 0.12f;
    [SerializeField]
    private float mapZ;
    [SerializeField]
    private Vector2 mapMin = new Vector2(-20, -12);
    [SerializeField]
    private Vector2 mapMax = new Vector2(20, 12);
    private Camera mapCamera;
    private bool isDragging;
    private Vector2 previousMousePosition;
    private void Awake()
    {
        mapCamera = GetComponent<Camera>();
        if (!mapCamera.orthographic)
        {
            Debug.LogError("Use uma câmera Orthographic.", this);
            enabled = false;
        }
    }

    private bool Point(Vector2 screen, out Vector3 point)
    {
        Ray ray = mapCamera.ScreenPointToRay(screen);
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, mapZ));
        if (plane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private void Update()
    {
        if (!Application.isFocused)
        {
            isDragging = false;
            return;
        }

        if (PontoInput.OverUI)
        {
            isDragging = false;
            return;
        }

        Vector2 movement = PontoInput.Movement;
        transform.position += new Vector3(movement.x, movement.y, 0) * moveSpeed * Time.unscaledDeltaTime;
        Vector2 mouse = PontoInput.MousePosition;
        bool inside = mapCamera.pixelRect.Contains(mouse);
        if (!PontoInput.MiddleHeld)
            isDragging = false;
        else if (!isDragging && inside)
        {
            isDragging = true;
            previousMousePosition = mouse;
        }
        else if (isDragging)
        {
            if (Point(previousMousePosition, out Vector3 old) && Point(mouse, out Vector3 current))
                transform.position += old - current;
            previousMousePosition = mouse;
        }

        if (inside && !Mathf.Approximately(PontoInput.Scroll, 0))
        {
            bool found = Point(mouse, out Vector3 before);
            mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize * Mathf.Exp(-Mathf.Sign(PontoInput.Scroll) * zoomStep), minZoom, maxZoom);
            ClampCameraToMap();
            if (found && Point(mouse, out Vector3 after))
                transform.position += before - after;
        }

        ClampCameraToMap();
    }

    private void ClampCameraToMap()
    {
        float left = Mathf.Min(mapMin.x, mapMax.x), right = Mathf.Max(mapMin.x, mapMax.x);
        float bottom = Mathf.Min(mapMin.y, mapMax.y), top = Mathf.Max(mapMin.y, mapMax.y);
        if (right <= left || top <= bottom)
            return;
        float aspect = Mathf.Max(mapCamera.aspect, 0.0001f);
        float max = Mathf.Min(maxZoom, (top - bottom) * 0.5f, (right - left) * 0.5f / aspect);
        mapCamera.orthographicSize = Mathf.Clamp(mapCamera.orthographicSize, Mathf.Min(minZoom, max), max);
        float h = mapCamera.orthographicSize, w = h * aspect;
        Vector3 p = transform.position;
        p.x = left + w <= right - w ? Mathf.Clamp(p.x, left + w, right - w) : (left + right) / 2;
        p.y = bottom + h <= top - h ? Mathf.Clamp(p.y, bottom + h, top - h) : (bottom + top) / 2;
        transform.position = p;
    }

    private void OnDisable()
    {
        isDragging = false;
    }

    private void OnApplicationFocus(bool focus)
    {
        isDragging = false;
    }
}
