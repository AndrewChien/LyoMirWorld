using System;
using System.Net.Sockets;

namespace MirCommon.Network
{
    /// <summary>
    /// 网络错误码
    /// </summary>
    public enum NetworkErrorCode
    {
        /// <summary>
        /// 操作成功
        /// </summary>
        ME_OK = 0,
        
        /// <summary>
        /// 操作失败
        /// </summary>
        ME_FAIL = -1,
        
        /// <summary>
        /// 套接字操作会阻塞
        /// </summary>
        ME_SOCKETWOULDBLOCK = -2,
        
        /// <summary>
        /// 套接字已关闭
        /// </summary>
        ME_SOCKETCLOSED = -3,
        
        /// <summary>
        /// 连接被拒绝
        /// </summary>
        ME_CONNECTIONREFUSED = -4,
        
        /// <summary>
        /// 连接超时
        /// </summary>
        ME_CONNECTIONTIMEOUT = -5,
        
        /// <summary>
        /// 网络不可达
        /// </summary>
        ME_NETWORKUNREACHABLE = -6,
        
        /// <summary>
        /// 主机不可达
        /// </summary>
        ME_HOSTUNREACHABLE = -7,
        
        /// <summary>
        /// 连接重置
        /// </summary>
        ME_CONNECTIONRESET = -8,
        
        /// <summary>
        /// 连接中止
        /// </summary>
        ME_CONNECTIONABORTED = -9,
        
        /// <summary>
        /// 缓冲区溢出
        /// </summary>
        ME_BUFFEROVERFLOW = -10,
        
        /// <summary>
        /// 无效参数
        /// </summary>
        ME_INVALIDPARAMETER = -11,
        
        /// <summary>
        /// 内存不足
        /// </summary>
        ME_OUTOFMEMORY = -12,
        
        /// <summary>
        /// 未知错误
        /// </summary>
        ME_UNKNOWN = -999
    }

    /// <summary>
    /// 网络错误处理类
    /// </summary>
    public class NetworkError
    {
        private NetworkErrorCode _errorCode;
        private string _errorMessage;

        public NetworkError()
        {
            _errorCode = NetworkErrorCode.ME_OK;
            _errorMessage = string.Empty;
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        public void SetError(NetworkErrorCode errorCode, string format, params object[] args)
        {
            _errorCode = errorCode;
            _errorMessage = string.Format(format, args);
        }

        /// <summary>
        /// 设置错误信息（从另一个NetworkError对象）
        /// </summary>
        public void SetError(NetworkError error)
        {
            if (error != null)
            {
                _errorCode = error._errorCode;
                _errorMessage = error._errorMessage;
            }
        }

        /// <summary>
        /// 从SocketException设置错误信息
        /// </summary>
        public void SetErrorFromSocketException(SocketException ex)
        {
            _errorCode = ConvertSocketErrorToNetworkError(ex.SocketErrorCode);
            _errorMessage = ex.Message;
        }

        /// <summary>
        /// 从Exception设置错误信息
        /// </summary>
        public void SetErrorFromException(Exception ex)
        {
            if (ex is SocketException socketEx)
            {
                SetErrorFromSocketException(socketEx);
            }
            else
            {
                _errorCode = NetworkErrorCode.ME_UNKNOWN;
                _errorMessage = ex.Message;
            }
        }

        /// <summary>
        /// 获取错误码
        /// </summary>
        public NetworkErrorCode GetErrorCode() => _errorCode;

        /// <summary>
        /// 获取错误信息
        /// </summary>
        public string GetErrorMessage() => _errorMessage;

        /// <summary>
        /// 清除错误信息
        /// </summary>
        public void Clear()
        {
            _errorCode = NetworkErrorCode.ME_OK;
            _errorMessage = string.Empty;
        }

        /// <summary>
        /// 检查是否成功
        /// </summary>
        public bool IsSuccess() => _errorCode == NetworkErrorCode.ME_OK;

        /// <summary>
        /// 检查是否失败
        /// </summary>
        public bool IsFailure() => _errorCode != NetworkErrorCode.ME_OK;

        /// <summary>
        /// 将SocketError转换为NetworkErrorCode
        /// </summary>
        public static NetworkErrorCode ConvertSocketErrorToNetworkError(SocketError socketError)
        {
            return socketError switch
            {
                SocketError.Success => NetworkErrorCode.ME_OK,
                SocketError.WouldBlock => NetworkErrorCode.ME_SOCKETWOULDBLOCK,
                SocketError.ConnectionRefused => NetworkErrorCode.ME_CONNECTIONREFUSED,
                SocketError.TimedOut => NetworkErrorCode.ME_CONNECTIONTIMEOUT,
                SocketError.NetworkUnreachable => NetworkErrorCode.ME_NETWORKUNREACHABLE,
                SocketError.HostUnreachable => NetworkErrorCode.ME_HOSTUNREACHABLE,
                SocketError.ConnectionReset => NetworkErrorCode.ME_CONNECTIONRESET,
                SocketError.ConnectionAborted => NetworkErrorCode.ME_CONNECTIONABORTED,
                SocketError.NoBufferSpaceAvailable => NetworkErrorCode.ME_BUFFEROVERFLOW,
                SocketError.InvalidArgument => NetworkErrorCode.ME_INVALIDPARAMETER,
                // SocketError.NoMemory 在 .NET 中不存在，使用 NoBufferSpaceAvailable 作为最接近的映射
                // 或者使用特定值 (SocketError)10012 对应 WSAENOMEM
                _ when (int)socketError == 10012 => NetworkErrorCode.ME_OUTOFMEMORY, // WSAENOMEM
                _ => NetworkErrorCode.ME_UNKNOWN
            };
        }

        /// <summary>
        /// 将WinSock错误码转换为NetworkErrorCode
        /// </summary>
        public static NetworkErrorCode ConvertWinSockErrorToNetworkError(int winSockError)
        {
            return winSockError switch
            {
                0 => NetworkErrorCode.ME_OK,
                (int)SocketError.WouldBlock => NetworkErrorCode.ME_SOCKETWOULDBLOCK,
                (int)SocketError.ConnectionRefused => NetworkErrorCode.ME_CONNECTIONREFUSED,
                (int)SocketError.TimedOut => NetworkErrorCode.ME_CONNECTIONTIMEOUT,
                (int)SocketError.NetworkUnreachable => NetworkErrorCode.ME_NETWORKUNREACHABLE,
                (int)SocketError.HostUnreachable => NetworkErrorCode.ME_HOSTUNREACHABLE,
                (int)SocketError.ConnectionReset => NetworkErrorCode.ME_CONNECTIONRESET,
                (int)SocketError.ConnectionAborted => NetworkErrorCode.ME_CONNECTIONABORTED,
                (int)SocketError.NoBufferSpaceAvailable => NetworkErrorCode.ME_BUFFEROVERFLOW,
                (int)SocketError.InvalidArgument => NetworkErrorCode.ME_INVALIDPARAMETER,
                10012 => NetworkErrorCode.ME_OUTOFMEMORY, // WSAENOMEM
                _ => NetworkErrorCode.ME_UNKNOWN
            };
        }

        /// <summary>
        /// 获取错误码的字符串表示
        /// </summary>
        public static string GetErrorCodeString(NetworkErrorCode errorCode)
        {
            return errorCode switch
            {
                NetworkErrorCode.ME_OK => "ME_OK",
                NetworkErrorCode.ME_FAIL => "ME_FAIL",
                NetworkErrorCode.ME_SOCKETWOULDBLOCK => "ME_SOCKETWOULDBLOCK",
                NetworkErrorCode.ME_SOCKETCLOSED => "ME_SOCKETCLOSED",
                NetworkErrorCode.ME_CONNECTIONREFUSED => "ME_CONNECTIONREFUSED",
                NetworkErrorCode.ME_CONNECTIONTIMEOUT => "ME_CONNECTIONTIMEOUT",
                NetworkErrorCode.ME_NETWORKUNREACHABLE => "ME_NETWORKUNREACHABLE",
                NetworkErrorCode.ME_HOSTUNREACHABLE => "ME_HOSTUNREACHABLE",
                NetworkErrorCode.ME_CONNECTIONRESET => "ME_CONNECTIONRESET",
                NetworkErrorCode.ME_CONNECTIONABORTED => "ME_CONNECTIONABORTED",
                NetworkErrorCode.ME_BUFFEROVERFLOW => "ME_BUFFEROVERFLOW",
                NetworkErrorCode.ME_INVALIDPARAMETER => "ME_INVALIDPARAMETER",
                NetworkErrorCode.ME_OUTOFMEMORY => "ME_OUTOFMEMORY",
                NetworkErrorCode.ME_UNKNOWN => "ME_UNKNOWN",
                _ => $"UNKNOWN_ERROR({(int)errorCode})"
            };
        }

        /// <summary>
        /// 获取完整的错误信息
        /// </summary>
        public string GetFullErrorMessage()
        {
            if (string.IsNullOrEmpty(_errorMessage))
                return GetErrorCodeString(_errorCode);
            
            return $"{GetErrorCodeString(_errorCode)}: {_errorMessage}";
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        public override string ToString()
        {
            return GetFullErrorMessage();
        }
    }

    /// <summary>
    /// 网络操作结果类
    /// </summary>
    public class NetworkResult
    {
        public NetworkErrorCode ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public int BytesTransferred { get; set; }
        public bool IsSuccess => ErrorCode == NetworkErrorCode.ME_OK;

        public NetworkResult()
        {
            ErrorCode = NetworkErrorCode.ME_OK;
            ErrorMessage = string.Empty;
            BytesTransferred = 0;
        }

        public NetworkResult(NetworkErrorCode errorCode, string errorMessage = "", int bytesTransferred = 0)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            BytesTransferred = bytesTransferred;
        }

        public static NetworkResult Success(int bytesTransferred = 0)
        {
            return new NetworkResult(NetworkErrorCode.ME_OK, "", bytesTransferred);
        }

        public static NetworkResult Failure(NetworkErrorCode errorCode, string errorMessage = "")
        {
            return new NetworkResult(errorCode, errorMessage);
        }

        public static NetworkResult FromException(Exception ex)
        {
            if (ex is SocketException socketEx)
            {
                var errorCode = NetworkError.ConvertSocketErrorToNetworkError(socketEx.SocketErrorCode);
                return new NetworkResult(errorCode, socketEx.Message);
            }
            else
            {
                return new NetworkResult(NetworkErrorCode.ME_UNKNOWN, ex.Message);
            }
        }
    }
}
