using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyScoreText : MonoBehaviour
{
    void Start()
    {
        Invoke(nameof(DestroyText), 2);
    }

    private void DestroyText()
    {
        Destroy(gameObject);
    }
}
