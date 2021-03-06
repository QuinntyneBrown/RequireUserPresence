import { Component, Inject, AfterViewInit } from "@angular/core";
import { Subject, BehaviorSubject } from "rxjs";
import { HttpClient } from "@angular/common/http";
import { AuthService } from "./auth.service";
import { Router } from "@angular/router";
import { map } from "rxjs/operators";
import { HubClient } from "./hub-client";

@Component({
  templateUrl: "./home-page.component.html",
  styleUrls: ["./home-page.component.css"],
  selector: "app-home-page"
})
export class HomePageComponent { 
  constructor(
    @Inject("apiUrl") private readonly _apiUrl:string,
    private readonly _authService: AuthService,
    private readonly _hubClient: HubClient,
    private readonly _httpClient: HttpClient,
    private readonly _router: Router
  ) {

  }

  ngOnInit() {    
    
    this._hubClient.events
    .pipe(map(x => this.pong$.next(x)))
    .subscribe();

    this._hubClient.usersOnline$
    .pipe(map(x => this.usersOnline$.next(x)))
    .subscribe();    
  }

  public tryToPing() {
    this._httpClient.post(`${this._apiUrl}api/ping`,null,{ headers: {
      "Authorization":`Bearer ${localStorage.getItem("accessToken")}`,
      "ConnectionId": localStorage.getItem("connectionId")
    } }).subscribe();
  }

  public usersOnline$: BehaviorSubject<string> = new BehaviorSubject("");

  public pong$:BehaviorSubject<string> = new BehaviorSubject("");
  
  public onDestroy: Subject<void> = new Subject<void>();

  ngOnDestroy() {
    this.onDestroy.next();	
  }

  public get username() { return localStorage.getItem("username"); }

  tryToSignOut() {
    this._authService.tryToSignOut();
    this._hubClient.disconnect();
    this._router.navigateByUrl("/login");
  }
}
