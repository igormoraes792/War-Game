#nullable enable
using UnityEngine;

namespace War.Presentation.CameraControl
{
    /// <summary>
    /// Controlador de câmera tática 3D estilo mesa de estratégia (RTS / Board Game).
    /// Suporta Pan suave (WASD / Setas / Arraste com RMB ou MMB), Zoom suave com scroll do mouse e Rotação de órbita ao redor do centro.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TacticalCameraController : MonoBehaviour
    {
        [Header("Alvo e Ponto Focal")]
        [Tooltip("Ponto central no plano do tabuleiro ao redor do qual a câmera orbita e translada.")]
        [SerializeField] private Vector3 pivotPosition = Vector3.zero;

        [Header("Velocidades e Sensibilidade")]
        [SerializeField] private float panSpeedKeyboard = 25f;
        [SerializeField] private float panSpeedMouse = 1.2f;
        [SerializeField] private float zoomSensitivity = 8f;
        [SerializeField] private float rotationSpeed = 3f;
        [SerializeField] private float smoothTime = 0.08f;

        [Header("Limites de Zoom (Distância)")]
        [SerializeField] private float minDistance = 12f;
        [SerializeField] private float maxDistance = 65f;
        [SerializeField] private float defaultDistance = 35f;

        [Header("Limites de Órbita e Ângulo")]
        [SerializeField] private float minPitch = 25f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float defaultPitch = 50f;
        [SerializeField] private float defaultYaw = 0f;

        [Header("Limites do Tabuleiro (Bounding Box)")]
        [SerializeField] private Vector2 panLimitX = new Vector2(-40f, 40f);
        [SerializeField] private Vector2 panLimitZ = new Vector2(-28f, 28f);

        // Estados alvo para interpolação suave
        private Vector3 _targetPivot;
        private float _targetDistance;
        private float _targetYaw;
        private float _targetPitch;

        // Velocidades de amortecimento
        private Vector3 _pivotVelocity;
        private float _distanceVelocity;
        private float _yawVelocity;
        private float _pitchVelocity;

        // Estado do mouse para pan e rotação
        private Vector3 _lastMousePos;

        private void Start()
        {
            _targetPivot = pivotPosition;
            _targetDistance = defaultDistance;
            _targetYaw = defaultYaw;
            _targetPitch = defaultPitch;

            UpdateCameraTransformImmediate();
        }

        private void Update()
        {
            HandleKeyboardInput();
            HandleMouseInput();
            HandleZoomInput();
        }

        private void LateUpdate()
        {
            SmoothCameraMotion();
        }

        private void HandleKeyboardInput()
        {
            var h = Input.GetAxisRaw("Horizontal");
            var v = Input.GetAxisRaw("Vertical");

            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                // Move o pivô no plano horizontal alinhado com a rotação atual da câmera
                var forward = transform.forward;
                forward.y = 0;
                forward.Normalize();

                var right = transform.right;
                right.y = 0;
                right.Normalize();

                var direction = (forward * v + right * h).normalized;
                _targetPivot += direction * (panSpeedKeyboard * Time.deltaTime);
                ClampTargetPivot();
            }

            // Teclas Q e E para rotação orbital rápida no teclado
            if (Input.GetKey(KeyCode.Q))
            {
                _targetYaw -= rotationSpeed * 30f * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.E))
            {
                _targetYaw += rotationSpeed * 30f * Time.deltaTime;
            }
        }

        private void HandleMouseInput()
        {
            // Pan arrastando com botão direito (RMB) ou botão do meio (MMB)
            var isPanDragging = Input.GetMouseButton(1) || Input.GetMouseButton(2);

            // Rotação orbital segurando Alt + LMB ou Ctrl + RMB
            var isRotateDragging = (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) && Input.GetMouseButton(0);

            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2) || (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0)))
            {
                _lastMousePos = Input.mousePosition;
            }

            if (isRotateDragging)
            {
                var delta = Input.mousePosition - _lastMousePos;
                _targetYaw += delta.x * rotationSpeed * 0.1f;
                _targetPitch -= delta.y * rotationSpeed * 0.1f;
                _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
                _lastMousePos = Input.mousePosition;
            }
            else if (isPanDragging)
            {
                var delta = Input.mousePosition - _lastMousePos;
                
                var forward = transform.forward;
                forward.y = 0;
                forward.Normalize();

                var right = transform.right;
                right.y = 0;
                right.Normalize();

                var moveFactor = panSpeedMouse * (_targetDistance / defaultDistance) * 0.05f;
                var move = -(right * delta.x + forward * delta.y) * moveFactor;
                _targetPivot += move;
                ClampTargetPivot();

                _lastMousePos = Input.mousePosition;
            }
        }

        private void HandleZoomInput()
        {
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _targetDistance -= scroll * zoomSensitivity * 10f;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }
        }

        private void ClampTargetPivot()
        {
            _targetPivot.x = Mathf.Clamp(_targetPivot.x, panLimitX.x, panLimitX.y);
            _targetPivot.z = Mathf.Clamp(_targetPivot.z, panLimitZ.x, panLimitZ.y);
            _targetPivot.y = 0f;
        }

        private void SmoothCameraMotion()
        {
            // Interpolação amortecida das variáveis alvo
            pivotPosition = Vector3.SmoothDamp(pivotPosition, _targetPivot, ref _pivotVelocity, smoothTime);
            defaultDistance = Mathf.SmoothDamp(defaultDistance, _targetDistance, ref _distanceVelocity, smoothTime);
            defaultYaw = Mathf.SmoothDamp(defaultYaw, _targetYaw, ref _yawVelocity, smoothTime);
            defaultPitch = Mathf.SmoothDamp(defaultPitch, _targetPitch, ref _pitchVelocity, smoothTime);

            var rotation = Quaternion.Euler(defaultPitch, defaultYaw, 0f);
            var offset = rotation * new Vector3(0f, 0f, -defaultDistance);

            transform.position = pivotPosition + offset;
            transform.LookAt(pivotPosition);
        }

        private void UpdateCameraTransformImmediate()
        {
            pivotPosition = _targetPivot;
            defaultDistance = _targetDistance;
            defaultYaw = _targetYaw;
            defaultPitch = _targetPitch;

            var rotation = Quaternion.Euler(defaultPitch, defaultYaw, 0f);
            var offset = rotation * new Vector3(0f, 0f, -defaultDistance);

            transform.position = pivotPosition + offset;
            transform.LookAt(pivotPosition);
        }

        /// <summary>
        /// Foca suavemente a câmera em uma coordenada específica no tabuleiro.
        /// </summary>
        public void FocusOnPosition(Vector3 worldPos)
        {
            _targetPivot = new Vector3(worldPos.x, 0f, worldPos.z);
            ClampTargetPivot();
        }

        /// <summary>
        /// Reseta a orientação da câmera para o centro da mesa com valores padrões.
        /// </summary>
        public void ResetView()
        {
            _targetPivot = Vector3.zero;
            _targetDistance = 35f;
            _targetYaw = 0f;
            _targetPitch = 50f;
        }
    }
}

