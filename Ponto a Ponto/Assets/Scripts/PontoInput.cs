using UnityEngine;

public static class PontoInput
{
    public static Vector2 MousePosition => Input.mousePosition;
    public static bool LeftClick => Input.GetMouseButtonDown(0);
    public static bool LeftHeld => Input.GetMouseButton(0);
    public static bool MiddleHeld => Input.GetMouseButton(2);
    public static float Scroll => Input.mouseScrollDelta.y;
    public static bool Pressed(KeyCode code)
    {
        if (PontoOptionsMenu.Instance != null && PontoOptionsMenu.Instance.BlocksGameplayInput)
            return false;
        return RawPressed(code);
    }

    public static bool RawPressed(KeyCode code) => Input.GetKeyDown(code);
    public static Vector2 Movement
    {
        get
        {
            if (PontoOptionsMenu.Instance != null && PontoOptionsMenu.Instance.BlocksGameplayInput)
                return Vector2.zero;
            Vector2 movement = Vector2.zero;
            if (Input.GetKey(KeyCode.W))
                movement.y++;
            if (Input.GetKey(KeyCode.S))
                movement.y--;
            if (Input.GetKey(KeyCode.A))
                movement.x--;
            if (Input.GetKey(KeyCode.D))
                movement.x++;
            return Vector2.ClampMagnitude(movement, 1f);
        }
    }

    public static bool OverUI => (PontoOptionsMenu.Instance != null && PontoOptionsMenu.Instance.ContainsMouse()) || (PontoDayOneTutorial.Instance != null && PontoDayOneTutorial.Instance.ContainsMouse()) || (PontoHUD.Instance != null && PontoHUD.Instance.ContainsMouse());
    public static bool MapPoint(Camera camera, float z, out Vector3 point)
    {
        point = Vector3.zero;
        if (camera == null || !camera.pixelRect.Contains(MousePosition))
            return false;
        Ray ray = camera.ScreenPointToRay(MousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0, 0, z));
        if (!plane.Raycast(ray, out float distance))
            return false;
        point = ray.GetPoint(distance);
        return true;
    }
}
