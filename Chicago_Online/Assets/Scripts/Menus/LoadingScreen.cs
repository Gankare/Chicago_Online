using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    private float timer;
    public Slider loadingBar;

    void Start()
    {
        timer = 0;
    }

    void Update()
    {
        timer += Time.deltaTime;
        loadingBar.value = timer;
        if (timer > 2)
            Destroy(gameObject);
    }
}
