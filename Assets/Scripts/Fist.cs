using UnityEngine;
using HelloWorld;

public class Fist : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 10f;
    public bool isUppercut = false;

    private Vector3 initialPosition;
    private bool isLaunched = false;

    private void Start()
    {
        initialPosition = transform.localPosition;
    }

    private void Update()
    {
        if (isLaunched)
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
    }

    public void Launch(float chargeTime, float damage, bool isUppercut)
    {
        this.damage = damage;
        this.isUppercut = isUppercut;
        isLaunched = true;
        Invoke(nameof(ResetFist), chargeTime);
    }

    private void ResetFist()
    {
        isLaunched = false;
        transform.localPosition = initialPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Fist hit player!");
            var player = other.GetComponent<HelloWorldPlayer>();
            if (player != null)
            {
                player.TakeDamageServerRpc((int)damage);
                if (isUppercut)
                {
                    var rb = other.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
                    }
                }
            }
            ResetFist();
        }
    }
}