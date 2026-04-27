using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    public float interactDistance = 3f;

    public Transform cameraTransform;

    float xRotation = 0f;

    private void Start()
    {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (InteractionUI.Instance != null && InteractionUI.Instance.isActiveAndEnabled)
            return;
        if (Input.GetKeyDown(KeyCode.E))
            TryOpenInteractionAsync();

        HandleMovement();
    }

    public async Task TryOpenInteractionAsync()
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            NPCInteractionReceiver npc = hit.collider.GetComponent<NPCInteractionReceiver>();
            if (npc != null)
            {
                // Default action for opening dialogue: Talk
                await InteractionUI.Instance.OpenAsync(npc, PlayerAction.Talk);
            }
        }
    }

    private void HandleMovement()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // Movement
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * moveSpeed * Time.deltaTime;
    }
}
