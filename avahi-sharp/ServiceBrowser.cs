/* $Id$ */

/***
  This file is part of avahi.

  avahi is free software; you can redistribute it and/or modify it
  under the terms of the GNU Lesser General Public License as
  published by the Free Software Foundation; either version 2.1 of the
  License, or (at your option) any later version.

  avahi is distributed in the hope that it will be useful, but WITHOUT
  ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
  or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General
  Public License for more details.

  You should have received a copy of the GNU Lesser General Public
  License along with avahi; if not, write to the Free Software
  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307
  USA.
***/

using System;
using System.Net;
using System.Collections;
using System.Runtime.InteropServices;

namespace Avahi
{
    internal delegate void ServiceBrowserCallback (IntPtr browser, int iface, Protocol proto, BrowserEvent bevent,
                                                   IntPtr name, IntPtr type, IntPtr domain, LookupResultFlags flags,
                                                   IntPtr userdata);
    
    public struct ServiceInfo
    {
        public int NetworkInterface;
        public Protocol Protocol;
        public string Domain;
        public string ServiceType;
        public string Name;

        public string HostName;
        public IPAddress Address;
        public UInt16 Port;
        public byte[][] Text;
        public LookupResultFlags Flags;

        public static ServiceInfo Zero = new ServiceInfo ();
    }

    public delegate void ServiceInfoHandler (object o, ServiceInfo info);
    
    public class ServiceBrowser : IDisposable
    {
        private IntPtr handle;
        private ArrayList infos = new ArrayList ();
        private Client client;
        private int iface;
        private Protocol proto;
        private string domain;
        private string type;
        private LookupFlags flags;
        private ServiceBrowserCallback cb;

        private ArrayList addListeners = new ArrayList ();
        private ArrayList removeListeners = new ArrayList ();
        
        [DllImport ("avahi-client")]
        private static extern IntPtr avahi_service_browser_new (IntPtr client, int iface, int proto, IntPtr type,
                                                                IntPtr domain, LookupFlags flags,
                                                                ServiceBrowserCallback cb,
                                                                IntPtr userdata);

        [DllImport ("avahi-client")]
        private static extern void avahi_service_browser_free (IntPtr handle);

        public event ServiceInfoHandler ServiceAdded
        {
            add {
                addListeners.Add (value);
                Start ();
            }
            remove {
                addListeners.Remove (value);
                Stop (false);
            }
        }
        
        public event ServiceInfoHandler ServiceRemoved
        {
            add {
                removeListeners.Add (value);
                Start ();
            }
            remove {
                removeListeners.Remove (value);
                Stop (false);
            }
        }

        public ServiceInfo[] Services
        {
            get { return (ServiceInfo[]) infos.ToArray (typeof (ServiceInfo)); }
        }

        public ServiceBrowser (Client client, string type) : this (client, type, client.DomainName)
        {
        }
        
        public ServiceBrowser (Client client, string type, string domain) : this (client, -1, Protocol.Unspecified,
                                                                                  type, domain, LookupFlags.None)
        {
        }
        
        public ServiceBrowser (Client client, int iface, Protocol proto, string type, string domain, LookupFlags flags)
        {
            this.client = client;
            this.iface = iface;
            this.proto = proto;
            this.domain = domain;
            this.type = type;
            this.flags = flags;
            cb = OnServiceBrowserCallback;
        }

        ~ServiceBrowser ()
        {
            Dispose ();
        }

        public void Dispose ()
        {
            Stop (true);
        }

        private void Start ()
        {
            if (client.Handle == IntPtr.Zero || handle != IntPtr.Zero ||
                (addListeners.Count == 0 && removeListeners.Count == 0))
                return;

            IntPtr domainPtr = Utility.StringToPtr (domain);
            IntPtr typePtr = Utility.StringToPtr (type);

            lock (client) {
                handle = avahi_service_browser_new (client.Handle, iface, (int) proto, typePtr, domainPtr, flags,
                                                    cb, IntPtr.Zero);
            }
            Utility.Free (domainPtr);
            Utility.Free (typePtr);
        }

        private void Stop (bool force)
        {
            if (client.Handle != IntPtr.Zero && handle != IntPtr.Zero &&
                (force || (addListeners.Count == 0 && removeListeners.Count == 0))) {
                
                lock (client) {
                    avahi_service_browser_free (handle);
                    handle = IntPtr.Zero;
                }
            }
        }

        private void OnServiceBrowserCallback (IntPtr browser, int iface, Protocol proto, BrowserEvent bevent,
                                               IntPtr name, IntPtr type, IntPtr domain, LookupResultFlags flags,
                                               IntPtr userdata)
        {

            ServiceInfo info;
            info.NetworkInterface = iface;
            info.Protocol = proto;
            info.Domain = Utility.PtrToString (domain);
            info.ServiceType = Utility.PtrToString (type);
            info.Name = Utility.PtrToString (name);
            info.HostName = null;
            info.Address = null;
            info.Port = 0;
            info.Text = null;
            info.Flags = flags;

            infos.Add (info);
            
            if (bevent == BrowserEvent.Added) {
                infos.Add (info);

                foreach (ServiceInfoHandler handler in addListeners)
                    handler (this, info);
            } else {
                infos.Remove (info);

                foreach (ServiceInfoHandler handler in removeListeners)
                    handler (this, info);
            }
        }
    }
}
