﻿using NetworkSocket.Core;
using NetworkSocket.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetworkSocket.Fast
{
    /// <summary>
    /// FastTcp公共类
    /// </summary>
    internal static class Common
    {
        /// <summary>
        /// 获取服务类型的Api行为
        /// </summary>
        /// <param name="seviceType">服务类型</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        public static IEnumerable<ApiAction> GetServiceApiActions(Type seviceType)
        {
            return seviceType
                .GetMethods()
                .Where(item => Attribute.IsDefined(item, typeof(ApiAttribute)))
                .Select(method => new ApiAction(method));
        }

        /// <summary>
        /// 设置Api行为返回的任务结果
        /// </summary>
        /// <param name="requestContext">上下文</param>
        /// <param name="taskSetActionTable">任务行为表</param>
        /// <param name="serializer">序列化工具</param>
        /// <returns></returns>
        public static bool SetApiActionTaskResult(RequestContext requestContext, TaskSetActionTable taskSetActionTable, ISerializer serializer)
        {
            var taskSetAction = taskSetActionTable.Take(requestContext.Packet.Id);
            if (taskSetAction == null)
            {
                return true;
            }

            try
            {
                var bytes = requestContext.Packet.Body;
                var value = serializer.Deserialize(bytes, taskSetAction.ValueType);
                return taskSetAction.SetResult(value);
            }
            catch (SerializerException ex)
            {
                return taskSetAction.SetException(ex);
            }
            catch (Exception ex)
            {
                return taskSetAction.SetException(new SerializerException(ex));
            }
        }


        /// <summary>
        /// 设置Api行为返回的任务异常        
        /// </summary>
        /// <param name="taskSetActionTable">任务行为表</param>
        /// <param name="requestContext">请求上下文</param>
        /// <returns></returns>
        public static bool SetApiActionTaskException(TaskSetActionTable taskSetActionTable, RequestContext requestContext)
        {
            var taskSetAction = taskSetActionTable.Take(requestContext.Packet.Id);
            if (taskSetAction == null)
            {
                return true;
            }

            var exceptionBytes = requestContext.Packet.Body;
            var message = exceptionBytes == null ? string.Empty : Encoding.UTF8.GetString(exceptionBytes);
            var exception = new RemoteException(message);
            return taskSetAction.SetException(exception);
        }

        /// <summary>       
        /// 发送异常信息到远程端
        /// </summary>
        /// <param name="session">会话对象</param>       
        /// <param name="exceptionContext">上下文</param>  
        /// <returns></returns>
        public static bool SendRemoteException(ISession session, ExceptionContext exceptionContext)
        {
            try
            {
                var packet = exceptionContext.Packet;
                packet.IsException = true;
                packet.Body = Encoding.UTF8.GetBytes(exceptionContext.Exception.Message);
                session.Send(packet.ToByteRange());
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 调用远程端的Api     
        /// 并返回结果数据任务
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="session">会话对象</param>
        /// <param name="taskSetActionTable">任务行为表</param>
        /// <param name="serializer">序列化工具</param>      
        /// <param name="packet">封包</param>      
        /// <exception cref="SocketException"></exception>   
        /// <returns></returns>
        public static Task<T> InvokeApi<T>(ISession session, TaskSetActionTable taskSetActionTable, ISerializer serializer, FastPacket packet)
        {
            var taskSource = new TaskCompletionSource<T>();
            var taskSetAction = new TaskSetAction<T>(taskSource);
            taskSetActionTable.Add(packet.Id, taskSetAction);

            session.Send(packet.ToByteRange());
            return taskSource.Task;
        }

        /// <summary>
        /// 获取和更新ActionContext的参数值
        /// </summary>
        /// <param name="serializer">序列化工具</param>
        /// <param name="actionContext">Api执行上下文</param>
        /// <returns></returns>
        public static object[] GetAndUpdateParameterValues(ISerializer serializer, ActionContext actionContext)
        {
            var action = actionContext.Action;
            var packet = actionContext.Packet;
            var bodyParameters = packet.GetBodyParameters();
            var parameters = new object[bodyParameters.Count];

            for (var i = 0; i < bodyParameters.Count; i++)
            {
                var parameterBytes = bodyParameters[i];
                var parameterType = action.ParameterTypes[i];

                if (parameterBytes == null || parameterBytes.Length == 0)
                {
                    parameters[i] = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
                }
                else
                {
                    parameters[i] = serializer.Deserialize(parameterBytes, parameterType);
                }
            }

            action.ParameterValues = parameters;
            return parameters;
        }
    }
}
