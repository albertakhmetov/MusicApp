/*  Copyright © 2025, Albert Akhmetov <akhmetov@live.com>   
 *
 *  This file is part of MusicApp.
 *
 *  MusicApp is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  MusicApp is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with MusicApp. If not, see <https://www.gnu.org/licenses/>.   
 *
 */
namespace MusicApp.Core;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

public interface ILazyDependency<T> where T : notnull
{
    T Resolve();

    static ILazyDependency<T> Create<TInstance>(
        IServiceProvider serviceProvider,
        object? serviceKey = null) where TInstance : notnull, T
    {
        return new LazyDependency<TInstance>(serviceProvider, serviceKey);
    }

    static ILazyDependency<T> Create(
        IServiceProvider serviceProvider,
        object? serviceKey = null)
    {
        return new LazyDependency<T>(serviceProvider, serviceKey);
    }

    private class LazyDependency<TInstance> : ILazyDependency<T> where TInstance : notnull, T
    {
        private readonly IServiceProvider serviceProvider;
        private readonly object? serviceKey;

        public LazyDependency(IServiceProvider serviceProvider, object? serviceKey)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            this.serviceProvider = serviceProvider;
            this.serviceKey = serviceKey;
        }

        public T Resolve() => serviceKey is null
            ? serviceProvider.GetRequiredService<TInstance>()
            : serviceProvider.GetRequiredKeyedService<TInstance>(serviceKey);
    }
}

public static class LazyDependencyExtensions
{
    public static IServiceCollection AddLazySingleton<T>(this IServiceCollection services) where T : notnull
    {
        return services
            .AddSingleton(serviceProvider => ILazyDependency<T>.Create(serviceProvider));
    }

    public static IServiceCollection AddLazyTransient<T>(this IServiceCollection services) where T : notnull
    {
        return services
            .AddTransient(serviceProvider => ILazyDependency<T>.Create(serviceProvider));
    }

    public static IServiceCollection AddLazyScoped<T>(this IServiceCollection services) where T : notnull
    {
        return services
            .AddScoped(serviceProvider => ILazyDependency<T>.Create(serviceProvider));
    }

    public static IServiceCollection AddLazyKeyedSingleton<T>(this IServiceCollection services, object? serviceKey) where T : notnull
    {
        return services
            .AddKeyedSingleton(serviceKey, (serviceProvider, serviceKey) => ILazyDependency<T>.Create(serviceProvider, serviceKey));
    }

    public static IServiceCollection AddLazyKeyedTransient<T>(this IServiceCollection services, object? serviceKey) where T : notnull
    {
        return services
            .AddKeyedSingleton(
                serviceKey, 
                (serviceProvider, serviceKey) => ILazyDependency<T>.Create(serviceProvider, serviceKey));
    }

    public static IServiceCollection AddLazyKeyedScoped<T>(this IServiceCollection services, object? serviceKey) where T : notnull
    {
        return services
            .AddKeyedSingleton(
                serviceKey, 
                (serviceProvider, serviceKey) => ILazyDependency<T>.Create(serviceProvider, serviceKey));
    }
}
