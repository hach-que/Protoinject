﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Protoinject
{
    internal class DefaultHierarchy : IHierarchy
    {
        private List<INode> _rootNodes;

        private ConditionalWeakTable<object, List<INode>> _lookupCache;

        public DefaultHierarchy()
        {
            _rootNodes = new List<INode>();
            _lookupCache = new ConditionalWeakTable<object, List<INode>>();
        }

        IReadOnlyCollection<INode> IHierarchy.RootNodes => _rootNodes.AsReadOnly();
        public int LookupCacheObjectCount => 0;

        public INode Lookup(object obj)
        {
            List<INode> value;
            if (_lookupCache.TryGetValue(obj, out @value))
            {
                return @value.FirstOrDefault();
            }

            return null;
        }

        public void AddRootNode(INode node)
        {
            _rootNodes.Add(node);
            AddNodeToLookup(node);
        }

        public void AddChildNode(IPlan parent, INode child)
        {
            if (!((INode) parent).Children.Contains(child))
            {
                ((DefaultNode) parent).AddChild(child);
            }

            AddNodeToLookup(child);
        }

        public void MoveNode(IPlan newParent, INode child)
        {
            RemoveNode(child);
            AddChildNode(newParent, child);

            // Since we're moving the node, update it's parent.  We don't
            // always do this on AddChildNode / RemoveNode, because we might
            // be adding a reference from one node to a node that is already
            // parented to something else.
            ((DefaultNode)child).Parent = (INode)newParent;
        }

        public void RemoveRootNode(INode node)
        {
            _rootNodes.Remove(node);
        }

        public void RemoveChildNode(IPlan parent, INode child)
        {
            ((DefaultNode)parent).RemoveChild(child);
        }

        public void RemoveNode(INode node)
        {
            if (node == null)
            {
                return;
            }

            if (_rootNodes.Contains(node))
            {
                RemoveRootNode(node);
            }
            else if (node.Parent != null)
            {
                RemoveChildNode(node.Parent, node);
            }
        }

        public void ChangeObjectOnNode(INode node, object newValue)
        {
            if (((DefaultNode) node).Type == null)
            {
                throw new InvalidOperationException("You can't change the object on this node, because no type has been assigned to this node.");
            }
            if (!((DefaultNode) node).Type.IsInstanceOfType(newValue))
            {
                throw new InvalidOperationException("The passed value needs to be an instance of or derive from " + ((DefaultNode)node).Type.FullName + ", but it does not.");
            }
            
            ((DefaultNode)node).UntypedValue = newValue;
            AddNodeToLookup(node);
        }

        public INode CreateNodeForObject(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

#if DEBUG
            if (Lookup(obj) != null)
            {
                throw new InvalidOperationException("You must not call CreateNodeForObject if there is already a node associated with this object in the hierarchy.  Call Lookup instead.");
            }
#endif

            var nodeToCreate = typeof(DefaultNode<>).MakeGenericType(obj.GetType());
            var createdNode = (DefaultNode)Activator.CreateInstance(nodeToCreate);
            createdNode.UntypedValue = obj;
            createdNode.Type = obj.GetType();
            createdNode.Discarded = false;
            AddNodeToLookup(createdNode);
            return createdNode;
        }

        private void AddNodeToLookup(INode node)
        {
            if (node.UntypedValue != null)
            {
                var list = _lookupCache.GetOrCreateValue(node.UntypedValue);
                list.Add(node);
            }

            foreach (var child in node.Children)
            {
                AddNodeToLookup(child);
            }
        }
    }
}