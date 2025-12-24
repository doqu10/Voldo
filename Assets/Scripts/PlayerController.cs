using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float sprintSpeed = 8f;
    public float acceleration = 10f;
    private float currentSpeed;
    public float jumpHeight = 1.5f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 150f;
    public Transform cameraPivot;

    [Header("Head Bobbing")]
    public float walkBobSpeed = 10f;    // Yürürken sallanma hızı
    public float sprintBobSpeed = 14f;  // Koşarken sallanma hızı
    public float bobAmount = 0.05f;     // Sallanma mesafesi (yüksek değer çok sarsar)
    private float defaultYPos = 0;      // Kameranın normal yüksekliği
    private float timer = 0;            // Dalga hesabı için zamanlayıcı

    [Header("Gravity")]
    public float gravity = -9.81f;
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;
    bool isgrounded;
    private Vector3 velocity;

    float xRotation = 0f;
    float recoil = 0f;
    CharacterController controller;
    PlayerHealth playerHealth;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
        currentSpeed = moveSpeed;
        defaultYPos = cameraPivot.localPosition.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Move();
        Look();
        Jump();
        Run();
        HeadBob();
    }
     public void AddRecoil(float amount)
    {
         recoil += amount;
    }
    void Move()
    {
        //Yerçekimi kısmı
        isgrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask); //yere temas edip etmeme kontrolü
        if (isgrounded && velocity.y < 0) //yerle temas halindeyse ve aşağı doğru hız varsa
        {
            velocity.y = -2f; //küçük bir negatif değer veriyoruz ki yere yapışsın
        }
        

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);
        velocity.y += gravity * Time.deltaTime; //yerçekimi etkisi
        controller.Move(velocity * Time.deltaTime); //yerçekimi hareketi
    }

    void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        xRotation -= recoil;
        recoil = Mathf.Lerp(recoil, 0f, 10f * Time.deltaTime);
        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
        
    }

    void Jump()
    {
        if (isgrounded && Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(2f * -gravity * jumpHeight); //1.5f zıplama yüksekliği
            print("Jumped");
        }
    }

    void Run()
    {
        if(isgrounded)
        {
            // 1. Hedef hızı belirle (Shift'e basılıyorsa sprintSpeed, değilse moveSpeed)
            float targetSpeed;
            if(Input.GetKey(KeyCode.LeftShift) &&  (playerHealth.CurrentStamina > 0f))
            {
                targetSpeed = sprintSpeed;
                playerHealth.CurrentStamina -= 5f * Time.deltaTime; // Staminayı azalt
            }
            else
            {
                targetSpeed = moveSpeed;
                playerHealth.CurrentStamina += 5f * Time.deltaTime; // Staminayı arttır
            }

            // 2. Mevcut hızı (currentSpeed), hedef hıza (targetSpeed) Lerp ile yaklaştır
            // Buradaki '10f' değeri hızlanma/yavaşlanma ivmesidir, isteğine göre değiştirebilirsin.
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
        }
        else return;
        
    }
    void HeadBob()
    {
        // Koşma kontrolü: Hem bir hareket tuşuna basmalı hem de Shift'e basmalı
        bool isSprinting = (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f) 
                        && Input.GetKey(KeyCode.LeftShift);

        // Sadece koşarken ve yerdeyken salla
        if (isSprinting && isgrounded)
        {
            timer += Time.deltaTime * sprintBobSpeed;

            // Sallanma hesaplaması
            float newY = defaultYPos + Mathf.Sin(timer) * bobAmount;
            cameraPivot.localPosition = new Vector3(cameraPivot.localPosition.x, newY, cameraPivot.localPosition.z);
        }
        else
        {
            // Koşma bittiğinde veya durduğunda kamerayı yumuşakça eski yüksekliğine (merkeze) çek
            timer = 0;
            Vector3 targetPos = new Vector3(cameraPivot.localPosition.x, defaultYPos, cameraPivot.localPosition.z);
            cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, targetPos, Time.deltaTime * 5f);
        }
    }

}
