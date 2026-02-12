using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class TestController : MonoBehaviour
{
    private Animator anim;
    public WeaponSweepTrail sweepTrail; 
    public CancellationTokenSource attackCTS;
    void Awake()
    {
        anim = GetComponent<Animator>();
    }
    
    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            StartAttack().Forget();
        }
    }

    async UniTaskVoid StartAttack()
    {
        attackCTS?.Cancel();
        attackCTS?.Dispose();
        attackCTS = new CancellationTokenSource();
        
        try
        {
            anim.SetBool("isAttacking", true);
            await UniTask.Delay(1000, cancellationToken: attackCTS.Token);
            anim.SetBool("isAttacking", false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }
    
    public void OnAttackStart()
    {
        sweepTrail.isAttacking = true;
    }
    
    public void OnAttackEnd()
    {
        sweepTrail.isAttacking = false;
        sweepTrail.ResetAttack();
    }
}
