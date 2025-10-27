using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class playerscript : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public Animator animator;

    [Header("Interaction Settings")]
    public float interactRange = 1f;
    public LayerMask interactableLayer;

    private Rigidbody2D rb;
    private Vector2 movement;

    public SpriteRenderer sr;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if(ComfyImageCtr.avatarSprite != null)
        {
            sr.sprite = ComfyImageCtr.avatarSprite;
            sr.transform.localScale = new Vector3(0.2f, 0.2f, 1);
        }
    }

    void Update()
    {
        // Get movement input
        movement.x = Input.GetAxisRaw("Horizontal"); // A/D or Left/Right
        movement.y = Input.GetAxisRaw("Vertical");   // W/S or Up/Down

        if (Mathf.Abs(movement.x) > 0.05f || Mathf.Abs(movement.y) > 0.05f)
            animator.SetBool("moving", true);
        else
            animator.SetBool("moving", false);

        movement.Normalize(); // Prevent faster diagonal movement


        // Interaction input
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Interact();
        }
    }

    void FixedUpdate()
    {
        // Move the player
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    void Interact()
    {
        // Simple interaction logic: check for colliders in front of player
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRange, interactableLayer);
        foreach (var hit in hits)
        {
            Debug.Log($"Interacted with {hit.name}");
            // You could call an interface or method here on the interactable object
        }
    }

    // Optional: Draw interaction range in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
