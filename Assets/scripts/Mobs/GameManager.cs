using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Networked] public int SelectedBranchIndex { get; set; }

    private void Awake() => Instance = this;

    // Wywoływane z UI: np. 0 = lewa odnoga, 1 = prawa
    public void ChooseBranch(int index)
    {
        if (Runner == null) return;
        RPC_SetBranch(index);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetBranch(int index)
    {
        SelectedBranchIndex = index;
    }
}
