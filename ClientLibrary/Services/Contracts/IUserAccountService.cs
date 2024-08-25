﻿using BaseLibrary.Responses;
using BaseLibrary.DTOs;


namespace ClientLibrary.Services.Contracts
{
    public interface IUserAccountService
    {
        Task<GeneralResponse> CreateAsync(Register user);
        Task<LoginResponse> SignInAsync(Login user);
        Task<LoginResponse> RefreshTokenAsync(RefreshToken refreshToken);
        Task<WeatherForecast[]> GetWeatherForecasts();
    }
}
