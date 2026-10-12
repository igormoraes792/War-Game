#nullable enable
using UnityEngine;

namespace War2D.Presentation.CameraControl
{
    /// <summary>
    /// Controlador de Câmera 2D Ortográfica Tática.
    /// Suporta pan suave arrastando com botão direito/meio do mouse ou WASD/Setas,
    /// e zoom ortográfico clamped com scroll do mouse.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class CameraController2D : MonoBehaviour
    {
        [Header("Configurações de Pan")]
        [SerializeField] private float panSpeed = 16f;
        [SerializeField] private float smoothTime = 0.08f;
        [SerializeField] private Vector2 panLimitX = new Vector2(-15f, 15f);
        [SerializeField] private Vector2 panLimitY = new Vector2(-10f, 10f);

        [Header("Configurações de Zoom Ortográfico")]
        [SerializeField] private float minOrthoSize = 6.0f;
        [SerializeField] private float maxOrthoSize = 18.0f;
        [SerializeField] private float defaultOrthoSize = 13.5f;
        [SerializeField] private float zoomSensitivity = 3.5f;

        private Camera _camera = null!;
        private Vector3 _targetPosition;
        private float _targetOrthoSize;
        private Vector3 _positionVelocity;
        private float _zoomVelocity;
        private Vector3 _lastMousePos;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _targetOrthoSize = defaultOrthoSize;
            _camera.orthographicSize = defaultOrthoSize;
            _targetPosition = transform.position;
        }

        private void Update()
        {
            HandlePan();
            HandleZoom();
            ApplySmoothMotion();
        }

        private void HandlePan()
        {
            // Arrasto com botão direito (1) ou botão do meio (2)
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                _lastMousePos = Input.mousePosition;
            }
            else if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                Vector3 delta = Input.mousePosition - _lastMousePos;
                _lastMousePos = Input.mousePosition;

                // Fator proporcional ao tamanho ortográfico para sensação constante de arrasto
                float factor = (_camera.orthographicSize * 2f) / Screen.height;
                _targetPosition.x -= delta.x * factor;
                _targetPosition.y -= delta.y * factor;
            }

            // Teclado WASD / Setas
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                float speed = panSpeed * (_camera.orthographicSize / defaultOrthoSize) * Time.deltaTime;
                _targetPosition.x += h * speed;
                _targetPosition.y += v * speed;
            }

            // Clamp nos limites do mapa
            _targetPosition.x = Mathf.Clamp(_targetPosition.x, panLimitX.x, panLimitX.y);
            _targetPosition.y = Mathf.Clamp(_targetPosition.y, panLimitY.x, panLimitY.y);
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _targetOrthoSize -= scroll * zoomSensitivity * 4f;
                _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, minOrthoSize, maxOrthoSize);
            }
        }

        private void ApplySmoothMotion()
        {
            transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _positionVelocity, smoothTime);
            _camera.orthographicSize = Mathf.SmoothDamp(_camera.orthographicSize, _targetOrthoSize, ref _zoomVelocity, smoothTime);
        }

        public void FocusOn(Vector2 position)
        {
            _targetPosition.x = Mathf.Clamp(position.x, panLimitX.x, panLimitX.y);
            _targetPosition.y = Mathf.Clamp(position.y, panLimitY.x, panLimitY.y);
        }
    }
}
