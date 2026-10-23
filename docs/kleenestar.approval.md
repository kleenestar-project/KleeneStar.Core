![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Approvals

Changes, releases, access requests and purchases need a decision before they go on - often by several people, in a fixed order, depending on what is asked. **Approval policies** describe that process per class; **approvals** are its instances on objects, and their **decisions** are the audit trail.

## Policies

`ApprovalPolicy` (workspace, class, unique name, steps, state, `AllowSelfApproval`, `RequestStatusId`, `ApprovedStatusId`, `RejectedStatusId`). The steps are lines, passed in order:

```
<name>: <any | all | n of> <approvers> [if <WQL>] [within <hours>h]
```

|Part      |Meaning
|----------|--------------------------------------------------------------
|`any`     |one approver decides the step
|`all`     |every named account, and one member of every named group
|`n of`    |`n` distinct approvers
|approvers |user names, e-mail addresses, `group:<name>`; comma-separated
|`if`      |the step applies only to objects the WQL condition matches when the approval is requested (conditional approvals); a step that does not apply is passed over, and with none left the approval is approved at once
|`within`  |the step is overdue after so many hours (`approval.overdue`)

```
Change manager: any group:Change Managers within 24h
Change advisory board: 2 of group:CAB if Summary ~ "database" within 72h
```

A policy is validated in full on saving (`IApprovalManager.Validate`): readable lines, known approvers, valid WQL, a quorum that named accounts can reach, states of the class's workflow, and a request state that is not an end state of the approval.

**Templates** (`WebApproval/ApprovalTemplates`): *CAB* (change manager, then 2 of CAB), *E-CAB* (one E-CAB member within 4 h), *Four-eyes*. They fill the policy form (`Policy?template=cab`); the groups they name have to exist.

## Approvals

An approval is requested

- by the workflow: the object reaches the policy's request state (`ApprovalManager.Connect` on `TransitionExecuted`; the actor is the requester),
- by hand: *Request approval* on the object (needs the right to change it),
- by a rule: automation action `approval <policy>`.

A pending approval under the same policy is not requested twice. The steps are **resolved and frozen** (`Approval.Steps`, JSON): approvers to ids, conditions against the object - a later change to the policy does not change a running or a decided approval, and the record outlives the policy.

**Deciding.** Only an approver of the current step decides, once per step (unique `(Approval, Step, Identity)`); a rejection needs a reason and ends the approval; the requester does not decide unless the policy allows it (separation of duties). Two approvers deciding at once cannot both complete a step: `Approval.Version` is a concurrency token, and the later decision is taken again on the new state. Every decision is kept with approver, time and reason (`ApprovalDecision`) and recorded in the audit log (category `Workflow`), as are request and outcome.

**Outcome.** Approved or rejected, the object is moved to the policy's approved or rejected state, on behalf of the last decider; a move the workflow refuses is kept in `Approval.Outcome` and shown on the card. The requester is notified. The requester or a workspace administrator can withdraw a pending approval.

**Gate.** The workflow validator `approval.granted` lets a transition pass only when the object's latest (not withdrawn) approval is approved - for processes where a person moves the object after the approval.

**Notifications and escalation.** The approvers of a step get a notification when it begins (group members included, the requester excluded). Rules react to `approval.requested` (parameter: policy), `approval.completed` (`approved`, `rejected`, `cancelled`, or the policy) and `approval.overdue` (the step or the policy; raised once per step by the automation worker) - e.g. `escalate` or `notify` an overdue CAB.

## Surfaces

- **Object card** (issues/assets: properties; documents/blogs: below the content): the latest approvals, the step awaited, the decisions, and *Decide* / *Withdraw* / *Request approval: …* as the caller may.
- **Pages** `WWW/Approvals/Decide?approval=`, `Request?object=&policy=`, `Cancel?approval=`; policies under `WWW/ApprovalPolicies/{workspacekey}` (workspace cog → *Approvals*, area `approvalpolicies` = workspace administration).
- **REST** `/api/1/approvals` (`GET ?object=KEY`, `POST {object, policy, reason}`), `/api/1/approvals/decide {approval, verdict, reason}`, `/api/1/approvals/cancel {approval}`, `/api/1/approvalpolicies/{workspacekey}[/delete]`. An approval is reached through its object: whoever cannot read the object gets `404`. JSON and form posts are both accepted.
