// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Security.Util
{
    using System;
    using System.Collections;
    using System.Security.Permissions;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Diagnostics.CodeAnalysis;

    internal class TokenBasedSet
    {


        // Following 3 fields are used only for serialization compat purposes: DO NOT USE THESE EVER!
#pragma warning disable 414        
        private int      m_initSize = 24;
        private int      m_increment = 8;
#pragma warning restore 414        
        private Object[] m_objSet;
        //  END -> Serialization only fields

        [OptionalField(VersionAdded = 2)]        
        private volatile Object m_Obj;
        [OptionalField(VersionAdded = 2)]        
        private volatile Object[] m_Set;
        
        private int m_cElt;
        private volatile int m_maxIndex;


        internal bool MoveNext(ref TokenBasedSetEnumerator e)
        {
            switch (m_cElt)
            {
            case 0:
                return false;

            case 1:
                if (e.Index == -1)
                {
                    e.Index = m_maxIndex;
                    e.Current = m_Obj;
                    return true;
                }
                else
                {
                    e.Index = (short)(m_maxIndex+1);
                    e.Current = null;
                    return false;
                }

            default:
                while (++e.Index <= m_maxIndex)
                {
                    e.Current = Volatile.Read(ref m_Set[e.Index]);
                    
                    if (e.Current != null)
                        return true;
                }

                e.Current = null;
                return false;
            }
        }

        internal TokenBasedSet()
        {
            Reset();
        }

        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        internal TokenBasedSet(TokenBasedSet tbSet)
        {
            if (tbSet == null)
            {
                Reset();
                return;
            }

            if (tbSet.m_cElt > 1)
            {
                Object[] aObj = tbSet.m_Set;
                int aLen = aObj.Length;
                
                Object[] aNew = new Object[aLen];
                System.Array.Copy(aObj, 0, aNew, 0, aLen);
                
                m_Set = aNew;
            }
            else
            {
                m_Obj = tbSet.m_Obj;
            }

            m_cElt      = tbSet.m_cElt;
            m_maxIndex  = tbSet.m_maxIndex;
        }

        internal void Reset()
        {
            m_Obj = null;
            m_Set = null;
            m_cElt = 0;
            m_maxIndex = -1;
        }

        internal void SetItem(int index, Object item)
        {
            Object[] aObj = null;

            if (item == null)
            {
                RemoveItem(index);
                return;
            }

            switch (m_cElt)
            {
            case 0:
                // on the first item, we don't create an array, we merely remember it's index and value
                // this this the 99% case
                m_cElt = 1;
                m_maxIndex = (short)index;
                m_Obj = item;
                break;

            case 1:
                // we have to decide if a 2nd item has indeed been added and create the array
                // if it has
                if (index == m_maxIndex)
                {
                    // replacing the one existing item
                    m_Obj = item;
                 }
                else
                {
                    // adding a second distinct permission
                    Object objSaved = m_Obj;
                    int iMax = Math.Max(m_maxIndex, index);
                    
                    aObj = new Object[iMax+1];
                    aObj[m_maxIndex] = objSaved;
                    aObj[index] = item;
                    m_maxIndex = (short)iMax;
                    m_cElt = 2;
                    m_Set = aObj;
                    m_Obj = null;
                }
                break;

            default:
                // this is the general case code for when there is really an array

                aObj = m_Set;

                // we are now adding an item, check if we need to grow

                if (index >= aObj.Length)
                {
                    Object[] newset = new Object[index+1];
                    System.Array.Copy(aObj, 0, newset, 0, m_maxIndex+1);
                    m_maxIndex = (short)index;
                    newset[index] = item;
                    m_Set = newset;
                    m_cElt++;
                }
                else
                {
                    if (aObj[index] == null)
                        m_cElt++;

                    aObj[index] = item;

                    if (index > m_maxIndex)
                        m_maxIndex = (short)index;
                }
                break;
            }
        }

        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread-safety")]
        internal Object GetItem(int index)
        {
            switch (m_cElt)
            {
            case 0:
                return null;

            case 1:
                if (index == m_maxIndex)
                    return m_Obj;
                else
                    return null;
            default:
                if (index < m_Set.Length)
                    return Volatile.Read(ref m_Set[index]);
                else
                    return null;
            }
        }

        internal Object RemoveItem(int index)
        {
            Object ret = null;

            switch (m_cElt)
            {
            case 0:
                ret = null;
                break;

            case 1:
                if (index != m_maxIndex)
                {
                    // removing a permission we don't have ignore it
                    ret = null;
                }
                else 
                {
                    // removing the permission we have at the moment
                    ret = m_Obj;
                    Reset();
                }
                break;

            default:
                // this is the general case code for when there is really an array

                // we are removing an item                
                if (index < m_Set.Length && (ret = Volatile.Read(ref m_Set[index])) != null)
                {
                    // ok we really deleted something at this point

                    Volatile.Write(ref m_Set[index], null);
                    m_cElt--;

                    if (index == m_maxIndex)
                        ResetMaxIndex(m_Set);

                    // collapse the array
                    if (m_cElt == 1)
                    {
                        m_Obj = Volatile.Read(ref m_Set[m_maxIndex]);
                        m_Set = null;
                    }
                }
                break;
            }

            return ret;
        }

        private void ResetMaxIndex(Object[] aObj)
        {
            int i;

            // Start at the end of the array, and
            // scan backwards for the first non-null
            // slot. That is the new maxIndex.
            for (i = aObj.Length - 1; i >= 0; i--)
            {
                if (aObj[i] != null)
                {
                    m_maxIndex = (short)i;
                    return;
                }
            }

            m_maxIndex = -1;
        }
        internal int GetStartingIndex()
        {
            if (m_cElt <= 1)
                return m_maxIndex;
            return 0;
        }
        internal int GetCount()
        {
            return m_cElt;
        }

        internal int GetMaxUsedIndex()
        {
            return m_maxIndex;
        }

        internal bool FastIsEmpty()
        {
            return m_cElt == 0;
        }
    }
}
