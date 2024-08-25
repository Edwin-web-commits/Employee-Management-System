﻿using BaseLibrary.DTOs;
using BaseLibrary.Entities;
using BaseLibrary.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServerLibrary.Data;
using ServerLibrary.Helpers;
using ServerLibrary.Repositories.Contracts;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace ServerLibrary.Repositories.Implementations
{
    public class UserAccountRepository(IOptions<JwtSection> config, AppDbContext appDbContext) : IUserAccount
    {
        public async Task<GeneralResponse> CreateAsync(Register user)
        {
            if(user == null)
            {
                return new GeneralResponse(false, "Model is empty");
            }

            var checkUser = await FindUserByEmail(user.Email!);
            if(checkUser != null) 
            {
                return new GeneralResponse(false, "User registered already");
            }

            //Save user
            var applicationUser = await AddToDatabase(new ApplicationUser()
            {
                FullName = user.Fullname,
                Email = user.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(user.Password)
            });

            //check, create and assign role
            var checkAdminRole = await appDbContext.SystemRoles.FirstOrDefaultAsync(role => role.Name!.Equals(Constants.Admin));
            if(checkAdminRole is null) 
            {
                var createAdminRole = await AddToDatabase(new SystemRole() { Name = Constants.Admin });
                await AddToDatabase(new UserRole() { RoleId = createAdminRole.Id, UserId = applicationUser.Id });
                return new GeneralResponse(true, "Account created!");
            }

            var checkUserRole = await appDbContext.SystemRoles.FirstOrDefaultAsync(role => role.Name!.Equals(Constants.User));
            SystemRole response = new();
            if (checkUserRole is null)
            {
                 response = await AddToDatabase(new SystemRole() { Name = Constants.User });
                await AddToDatabase(new UserRole() { RoleId = response.Id, UserId = applicationUser.Id });
               
            }
            else
            {
                await AddToDatabase(new UserRole() { RoleId = checkUserRole.Id, UserId = applicationUser.Id });
               
            }
            return new GeneralResponse(true, "Account created!");

        }

        public async Task<LoginResponse> SignInAsync(Login user)
        {
                 if (user == null)
                 {
                    return new LoginResponse(false, "Model is empty");
                 }

                var applicationUser = await FindUserByEmail(user.Email);
                if (applicationUser == null)
                {
                    return new LoginResponse(false, "User not found");
                }
                    //Veify password
                if(!BCrypt.Net.BCrypt.Verify(user.Password, applicationUser.Password))
                {
                        return new LoginResponse(false, "Email/Password not valid");
                }
                
            var getUserRole = await FindUserRole( applicationUser.Id);
            if (getUserRole == null)
            {
                return new LoginResponse(false, "User role not found");
            }

            var getRoleName = await FindRoleName(getUserRole.RoleId);
            if (getRoleName == null)
            {
                return new LoginResponse(false, "User role not found");
            }

            string jwtToken = GenerateToken(applicationUser, getRoleName!.Name!);
            string refreshToken = GenerateRefreshToken();

            //Save the refresh token to the database
            var existingUserRefreshToken = await appDbContext.RefreshTokenInfos.FirstOrDefaultAsync(rt => rt.UserId == applicationUser.Id);
            if (existingUserRefreshToken != null) 
            {
                existingUserRefreshToken!.Token = refreshToken;
                await appDbContext.SaveChangesAsync();
            }
            else
            {
                await AddToDatabase(new RefreshTokenInfo() { Token = refreshToken, UserId = applicationUser.Id });
            }
            return new LoginResponse(true, "Login successfully", jwtToken, refreshToken);
        }

        private string GenerateToken(ApplicationUser user, string role)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Value.Key!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var userClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName!),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Role, role!),
            };
            var token = new JwtSecurityToken(
                issuer: config.Value.Issuer,
                audience: config.Value.Audience,
                claims: userClaims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: credentials
                );

            return new JwtSecurityTokenHandler().WriteToken(token);

        }

        private async Task<UserRole> FindUserRole(int userId) =>
            await appDbContext.UserRoles.FirstOrDefaultAsync(role => role.UserId == userId);

        private async Task<SystemRole> FindRoleName(int roleId) =>
            await appDbContext.SystemRoles.FirstOrDefaultAsync(role => role.Id == roleId);

        private static string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        private async Task<ApplicationUser> FindUserByEmail(string email) =>
            await appDbContext.ApplicationUsers.FirstOrDefaultAsync(user => user.Email!.ToLower()!.Equals(email!.ToLower()));

        private async Task<T> AddToDatabase<T>(T model)
        {
            var result = appDbContext.Add(model!);
            await appDbContext.SaveChangesAsync();
            return (T)result.Entity;
        }

        public async Task<LoginResponse> RefreshTokenAsync(RefreshToken refreshToken)
        {
            if (refreshToken == null)
            {
                return new LoginResponse(false, "Model is empty");
            }
            var findToken = await appDbContext.RefreshTokenInfos.FirstOrDefaultAsync(item => item.Token!.Equals(refreshToken.Token));
            if (findToken == null)
            {
                return new LoginResponse(false, "Refresh token is required");
            }

            //get user details
            var user = await appDbContext.ApplicationUsers.FirstOrDefaultAsync(user => user.Id == findToken.UserId);
            if (user == null)
            {
                return new LoginResponse(false, "Refresh token could not be generated because user not found");
            }

            var userRole = await FindUserRole(user.Id);
            var roleName = await FindRoleName(userRole.RoleId);
            string jwtToken = GenerateToken(user, roleName.Name!);
            string newRefreshToken = GenerateRefreshToken();

            var updateRefreshToken = await appDbContext.RefreshTokenInfos.FirstOrDefaultAsync(rt => rt.UserId == user.Id);
            if(updateRefreshToken == null)
            {
                return new LoginResponse(false, "Refresh token could not be generated because user has not signed In");
            }

            updateRefreshToken.Token = newRefreshToken;
            await appDbContext.SaveChangesAsync();
            return new LoginResponse(true, "Token refreshed successfully", jwtToken, newRefreshToken);
        }
    }
}
