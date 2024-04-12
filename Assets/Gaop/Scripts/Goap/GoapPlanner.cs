using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IGoapPlanner
{
    ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null);
}

public class GoapPlanner : IGoapPlanner
{
    public ActionPlan Plan(GoapAgent agent, HashSet<AgentGoal> goals, AgentGoal mostRecentGoal = null)
    {
        // 目標を優先順位の高い順に並べる
        List<AgentGoal> orderedGoals = goals
            .Where(g => g.DesiredEffects.Any(b => !b.Evaluate()))
            .OrderByDescending(g => g == mostRecentGoal ? g.Priority - 0.01 : g.Priority)
            .ToList();

        // それぞれの目標を順番に解決していく
        foreach (var goal in orderedGoals)
        {
            Node goalNode = new Node(null, null, goal.DesiredEffects, 0);

            // ゴールへのパスが見つかれば、プランを返す
            if (FindPath(goalNode, agent.actions))
            {
                // goalNodeにleafがなく、実行するアクションがない場合は、別のゴールを試す
                if(goalNode.IsLeafDead) continue;

                Stack<AgentAction> actionStack = new Stack<AgentAction>();
                while(goalNode.Leaves.Count > 0)
                {
                    var cheapestLeaf = goalNode.Leaves.OrderBy(leaf => leaf.Cost).First();
                    goalNode = cheapestLeaf;
                    actionStack.Push(cheapestLeaf.Action);
                }

                return new ActionPlan(goal, actionStack, goalNode.Cost);
            }
        }

        Debug.LogWarning("No plan found");
        return null;
    }

    bool FindPath(Node parent, HashSet<AgentAction> actions)
    {
        // Order actions by cost, ascending
        var orderedActions = actions.OrderBy(a => a.Cost);

        foreach (var action in orderedActions)
        {
            var requiredEffects = parent.RequiredEffects;

            // Remove any effects that evalute to true, there is no action to take
            requiredEffects.RemoveWhere(b => b.Evaluate());

            // If there are no requird effects to fulfill, we have a plan
            if (requiredEffects.Count == 0)
            {
                return true;
            }

            if (action.Effects.Any(requiredEffects.Contains))
            {
                var newRequiredEffects = new HashSet<AgentBelief>(requiredEffects);
                newRequiredEffects.ExceptWith(action.Effects);
                newRequiredEffects.UnionWith(action.Preconditions);

                var newAvailableActions = new HashSet<AgentAction>(actions);
                newAvailableActions.Remove(action);

                var newNode = new Node(parent, action, newRequiredEffects, parent.Cost + action.Cost);

                // Explore the new node recursively
                if (FindPath(newNode, newAvailableActions))
                {
                    parent.Leaves.Add(newNode);
                    newRequiredEffects.ExceptWith(newNode.Action.Preconditions);
                }

                // If all effects at this depth have been satisfied, return true
                if (newRequiredEffects.Count == 0)
                {
                    return true;
                }
            }
        }
        return false;
    }
}

public class Node
{
    public Node Parent { get; }
    public AgentAction Action { get; }
    public HashSet<AgentBelief> RequiredEffects { get; }
    public List<Node> Leaves { get; }
    public float Cost { get; }

    public bool IsLeafDead => Leaves.Count == 0 && Action == null;

    public Node(Node parent, AgentAction action, HashSet<AgentBelief> effects, float cost)
    {
        Parent = parent;
        Action = action;
        RequiredEffects = new HashSet<AgentBelief>(effects);
        Leaves = new List<Node>();
        Cost = cost;
    }
}

public class ActionPlan
{
    public AgentGoal AgentGoal { get; }
    public Stack<AgentAction> Actions { get; }
    public float TotalCost { get; set; }

    public ActionPlan(AgentGoal goal, Stack<AgentAction> actions, float totalCost)
    {
        AgentGoal = goal;
        Actions = actions;
        TotalCost = totalCost;
    }

}

