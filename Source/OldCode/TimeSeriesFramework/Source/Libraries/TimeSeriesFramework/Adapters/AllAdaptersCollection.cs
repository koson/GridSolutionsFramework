﻿//******************************************************************************************************
//  AllAdaptersCollection.cs - Gbtc
//
//  Copyright © 2010, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  09/02/2010 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.ComponentModel;
using System.Data;

namespace TimeSeriesFramework.Adapters
{
    /// <summary>
    /// Represents a collection of all <see cref="IAdapterCollection"/> implementations (i.e., a collection of <see cref="IAdapterCollection"/>'s).
    /// </summary>
    /// <remarks>
    /// This collection allows all <see cref="IAdapterCollection"/> implementations to be managed as a group.
    /// </remarks>
    public class AllAdaptersCollection : AdapterCollectionBase<IAdapterCollection>
    {
        #region [ Constructors ]

        /// <summary>
        /// Constructs a new instance of the <see cref="AllAdaptersCollection"/>.
        /// </summary>
        public AllAdaptersCollection()
        {
            base.Name = "All Adapters Collection";
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets flag that detemines if <see cref="IAdapter"/> implementations are automatically initialized
        /// when they are added to the collection.
        /// </summary>
        /// <remarks>
        /// We don't auto-initialize collections added to the <see cref="AllAdaptersCollection"/> since no data source
        /// will be available when the collections are being created.
        /// </remarks>
        protected override bool AutoInitialize
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Initializes each <see cref="IAdapterCollection"/> implementation in this <see cref="AllAdaptersCollection"/>.
        /// </summary>
        public override void Initialize()
        {
            Initialized = false;

            lock (this)
            {
                foreach (IAdapterCollection item in this)
                {
                    try
                    {
                        item.Initialize();
                    }
                    catch (Exception ex)
                    {
                        OnProcessException(ex);
                    }
                }
            }
            
            Initialized = true;
        }

        /// <summary>
        /// Attempts to get any adapter in all collections with the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">ID of adapter to get.</param>
        /// <param name="adapter">Adapter reference if found; otherwise null.</param>
        /// <param name="adapterCollection">Adapter collection reference if <paramref name="adapter"/> is found; otherwise null.</param>
        /// <returns><c>true</c> if adapter with the specified <paramref name="id"/> was found; otherwise <c>false</c>.</returns>
        public bool TryGetAnyAdapterByID(uint id, out IAdapter adapter, out IAdapterCollection adapterCollection)
        {
            lock (this)
            {
                foreach (IAdapterCollection collection in this)
                {
                    if (collection.TryGetAdapterByID(id, out adapter))
                    {
                        adapterCollection = collection;
                        return true;
                    }
                }
            }

            adapter = null;
            adapterCollection = null;
            return false;
        }

        /// <summary>
        /// Attempts to get any adapter in all collections with the specified <paramref name="name"/>.
        /// </summary>
        /// <param name="name">Name of adapter to get.</param>
        /// <param name="adapter">Adapter reference if found; otherwise null.</param>
        /// <param name="adapterCollection">Adapter collection reference if <paramref name="adapter"/> is found; otherwise null.</param>
        /// <returns><c>true</c> if adapter with the specified <paramref name="name"/> was found; otherwise <c>false</c>.</returns>
        public bool TryGetAnyAdapterByName(string name, out IAdapter adapter, out IAdapterCollection adapterCollection)
        {
            lock (this)
            {
                foreach (IAdapterCollection collection in this)
                {
                    if (collection.TryGetAdapterByName(name, out adapter))
                    {
                        adapterCollection = collection;
                        return true;
                    }
                }
            }

            adapter = null;
            adapterCollection = null;
            return false;
        }

        /// <summary>
        /// Attempts to initialize (or reinitialize) an individual <see cref="IAdapter"/> based on its ID from any collection.
        /// </summary>
        /// <param name="id">The numeric ID associated with the <see cref="IAdapter"/> to be initialized.</param>
        /// <returns><c>true</c> if item was successfully initialized; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// This method traverses all collections looking for an adapter with the specified ID.
        /// </remarks>
        public override bool TryInitializeAdapterByID(uint id)
        {
            lock (this)
            {
                foreach (IAdapterCollection collection in this)
                {
                    if (collection.TryInitializeAdapterByID(id))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This method is not implemented in <see cref="AllAdaptersCollection"/>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool TryCreateAdapter(DataRow adapterRow, out IAdapterCollection adapter)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This method is not implemented in <see cref="AllAdaptersCollection"/>.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool TryGetAdapterByID(uint ID, out IAdapterCollection adapter)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}