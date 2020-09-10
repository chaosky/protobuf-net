// using System.Net;
// using System.Net.Sockets;

/// <summary>
/// 说明：针对IPEndPoint每次Serialize调用都会产生垃圾而封装的无GC版本
/// 
/// by wsh @ 2017-06-23
/// </summary>

namespace CustomDataStruct
{
    public sealed class BetterIPEndPoint : System.Net.EndPoint
    {
        System.Net.IPEndPoint mIPEndPoint;
        System.Net.SocketAddress mSocketAddress;
        bool mIsDirty = true;

        public BetterIPEndPoint(long iaddr, int port)
        {
            mIPEndPoint = new System.Net.IPEndPoint(iaddr, port);
        }

        public BetterIPEndPoint(System.Net.IPAddress address, int port)
        {
            mIPEndPoint = new System.Net.IPEndPoint(address, port);
        }

        public System.Net.IPAddress Address
        {
            get
            {
                return mIPEndPoint.Address;
            }
            set
            {
                if (mIPEndPoint.Address != value)
                {
                    mIPEndPoint.Address = value;
                    mIsDirty = true;
                }
            }
        }

        public override System.Net.Sockets.AddressFamily AddressFamily
        {
            get
            {
                return mIPEndPoint.AddressFamily;
            }
        }

        public int Port
        {
            get
            {
                return mIPEndPoint.Port;
            }
            set
            {
                if (mIPEndPoint.Port != value)
                {
                    mIPEndPoint.Port = value;
                    mIsDirty = true;
                }
            }
        }

        public override System.Net.EndPoint Create(System.Net.SocketAddress socketAddress)
        {
            mIsDirty = true;
            return mIPEndPoint.Create(socketAddress);
        }

        public bool Equals(BetterIPEndPoint other)
        {
            return ReferenceEquals(this, other);
        }

        public bool Equals(System.Net.IPEndPoint other)
        {
            return mIPEndPoint.Equals(other);
        }

        public override System.Net.SocketAddress Serialize()
        {
            if (mIsDirty)
            {
                mSocketAddress = mIPEndPoint.Serialize();
                mIsDirty = false;
            } 
            return mSocketAddress;
        }

        public override int GetHashCode()
        {
            return mIPEndPoint.GetHashCode();
        }

        public override string ToString()
        {
            return mIPEndPoint.ToString();
        }
    }
}